using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class StatementFinalizer
{
    private IList<IStatement> _statements;
    private bool _hasReturnValue;
    // With an output struct, an if/else at the end can share one return after it;
    // without, each branch returns its own value.
    private readonly bool _hasOutputStruct;
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;
    private readonly ISet<HlslTreeNode> _doubleValues;
    // Registers the caller reads on return (a linkage body's result); kept alive.
    private readonly HashSet<RegisterKey> _liveOut;

    // Per break/continue, the keys its loop carries out to a reader. Computed
    // before lowering destroys the evidence; see FindCarriedByJump.
    private readonly Dictionary<IStatement, HashSet<RegisterComponentKey>> _carriedByJump =
        new(ReferenceEqualityComparer.Instance);

    private StatementFinalizer(IList<IStatement> statements, bool hasReturnValue,
        bool hasOutputStruct, IntegerOperandAnalysis integerOperandAnalysis,
        ISet<HlslTreeNode> doubleValues, IEnumerable<RegisterKey> liveOut)
    {
        _statements = statements;
        _hasReturnValue = hasReturnValue;
        _hasOutputStruct = hasOutputStruct;
        _integerOperandAnalysis = integerOperandAnalysis;
        _doubleValues = doubleValues ?? new HashSet<HlslTreeNode>();
        _liveOut = liveOut == null ? [] : [.. liveOut];
    }

    public static void Finalize(IList<IStatement> statements, bool hasReturnValue,
        bool hasOutputStruct = false, IntegerOperandAnalysis integerOperandAnalysis = null,
        ISet<HlslTreeNode> doubleValues = null, IEnumerable<RegisterKey> liveOut = null)
    {
        var finalizer = new StatementFinalizer(statements, hasReturnValue, hasOutputStruct,
            integerOperandAnalysis, doubleValues, liveOut);
        finalizer.FinalizeStatements();
    }

    // Output lanes joined across an if and returned after later statements: no
    // graph node reads them, the return does.
    private readonly Dictionary<PhiNode, IfStatement> _returnedOutputJoins =
        new(ReferenceEqualityComparer.Instance);

    private void FinalizeStatements()
    {
        FindReturnedOutputJoins();
        RemoveUnusedAssignmentInputOutput();
        FindCarriedByJump();
        RemoveUnusedAssignments(_statements);
        InsertTempVariableAssignments(_statements);
        LowerResolvedPhis();
        LoopRecovery.Recover(_statements);
        if (_hasReturnValue)
        {
            LowerReturnedOutputJoins();
        }
        NameOutputLoadsBeforeWrites();
        SetReturnStatement(_statements);
        SplitUncarriedLoopValues();
        CoalesceCopies();
        LocalizeDeadJoins();
    }

    /// <summary>
    /// A variable assigned in one branch only and dead after the if is declared in
    /// that branch rather than above the if. Statements outside the branch revert to
    /// the register's entry value (or nothing). Outermost if first, so each level
    /// restores its own entry value. Runs last so liveness matches the writer's.
    /// </summary>
    private void LocalizeDeadJoins()
    {
        VariableLiveness liveness = VariableLiveness.Analyze(_statements, exact: true);
        List<IfStatement> ifStatements = [];
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is IfStatement ifStatement)
            {
                ifStatements.Add(ifStatement);
            }
        });
        foreach (IfStatement ifStatement in ifStatements)
        {
            foreach (var output in ifStatement.Outputs.ToList())
            {
                if (output.Value is not TempVariableNode variable
                    || (ifStatement.Inputs.TryGetValue(output.Key, out HlslTreeNode entry)
                        && ReferenceEquals(entry, variable))
                    || liveness.MayBeLiveAfter(variable, ifStatement)
                    || IsLoopClauseVariable(variable))
                {
                    continue;
                }
                IList<IStatement> branch = BranchAssigningOnly(ifStatement, variable);
                if (branch == null)
                {
                    continue;
                }
                HashSet<IStatement> inside = new(ReferenceEqualityComparer.Instance);
                new StatementVisitor(branch).Visit(statement => inside.Add(statement));
                new StatementVisitor(_statements).Visit(statement =>
                {
                    if (inside.Contains(statement))
                    {
                        return;
                    }
                    foreach (IDictionary<RegisterComponentKey, HlslTreeNode> map in new[] { statement.Outputs, statement.Inputs })
                    {
                        if (map.TryGetValue(output.Key, out HlslTreeNode held) && ReferenceEquals(held, variable))
                        {
                            if (entry != null)
                            {
                                map[output.Key] = entry;
                            }
                            else
                            {
                                map.Remove(output.Key);
                            }
                        }
                    }
                });
                TempAssignmentNode first = null;
                new StatementVisitor(branch).Visit(statement =>
                {
                    first ??= statement.Outputs.Values.OfType<TempAssignmentNode>()
                        .FirstOrDefault(assignment => ReferenceEquals(assignment.TempVariable, variable)
                            && VariableLiveness.Writes(statement).Contains(variable));
                });
                if (first != null)
                {
                    first.IsReassignment = false;
                }
            }
        }
    }

    // The one branch holding every assignment to the variable, or null.
    private IList<IStatement> BranchAssigningOnly(IfStatement ifStatement, TempVariableNode variable)
    {
        List<IStatement> assigning = [];
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (VariableLiveness.Writes(statement).Contains(variable))
            {
                assigning.Add(statement);
            }
        });
        if (assigning.Count == 0)
        {
            return null;
        }
        foreach (IList<IStatement> branch in new[] { ifStatement.TrueBody, ifStatement.FalseBody })
        {
            if (branch == null)
            {
                continue;
            }
            HashSet<IStatement> inside = new(ReferenceEqualityComparer.Instance);
            new StatementVisitor(branch).Visit(statement => inside.Add(statement));
            if (assigning.All(inside.Contains))
            {
                return branch;
            }
        }
        return null;
    }

    /// <summary>
    /// Merges a copy with its source where they never interfere (neither live where
    /// the other is assigned, except at the copies), e.g.
    /// `t1 = t0; for (...) { t1 += c; } t0 = t1;`. All of a statement's lanes or
    /// none, so a vector is never split between two variables.
    /// </summary>
    private void CoalesceCopies()
    {
        bool merged = true;
        while (merged)
        {
            merged = false;
            VariableLiveness liveness = VariableLiveness.Analyze(_statements);
            List<IStatement> statements = [];
            new StatementVisitor(_statements).Visit(statements.Add);
            foreach (IStatement statement in statements.OfType<AssignmentStatement>())
            {
                List<(TempVariableNode Copy, TempVariableNode Source)> copies = [.. statement.Outputs
                    .Where(output => !statement.Inputs.TryGetValue(output.Key, out HlslTreeNode input)
                        || !ReferenceEquals(input, output.Value))
                    .Select(output => output.Value)
                    .OfType<TempAssignmentNode>()
                    .Where(assignment => Unmoved(assignment.Value) is TempVariableNode source
                        && !ReferenceEquals(source, assignment.TempVariable))
                    .Select(assignment => (assignment.TempVariable, (TempVariableNode)Unmoved(assignment.Value)))];
                if (copies.Count == 0
                    || !copies.All(copy => CanCoalesce(copy.Copy, copy.Source, statement, statements, liveness))
                    || SplitsAVector([.. copies.Select(copy => copy.Copy)], statement, statements)
                    || SplitsAVector([.. copies.Select(copy => copy.Source)], statement, statements))
                {
                    continue;
                }
                foreach ((TempVariableNode copy, TempVariableNode source) in copies)
                {
                    ReplaceTempVariable(copy, source);
                    RemoveSelfCopies(source);
                }
                merged = true;
                break;
            }
        }
    }

    // Whether one instruction elsewhere assigns these lanes together with others of
    // its register: merging only some leaves a vector named in two parts.
    private static bool SplitsAVector(HashSet<TempVariableNode> variables, IStatement copyStatement,
        List<IStatement> statements)
    {
        return statements.Any(statement =>
        {
            if (ReferenceEquals(statement, copyStatement))
            {
                return false;
            }
            List<KeyValuePair<RegisterComponentKey, TempAssignmentNode>> assignments = [.. statement.Outputs
                .Where(output => output.Value is TempAssignmentNode)
                .Select(output => KeyValuePair.Create(output.Key, (TempAssignmentNode)output.Value))];
            return assignments.Any(inside => variables.Contains(inside.Value.TempVariable)
                && assignments.Any(outside => !variables.Contains(outside.Value.TempVariable)
                    && outside.Key.RegisterKey.Equals(inside.Key.RegisterKey)
                    && OneInstruction(Unmoved(inside.Value.Value), Unmoved(outside.Value.Value))));
        });
    }

    // Immediates are not stamped with their instruction; a register's two lanes of
    // immediates in one statement are taken as one mov.
    private static bool OneInstruction(HlslTreeNode a, HlslTreeNode b)
    {
        return HlslTreeNode.IsSameInstruction(a, b) || (a is ConstantNode && b is ConstantNode);
    }

    private static HlslTreeNode Unmoved(HlslTreeNode node)
    {
        while (node is MoveOperation move)
        {
            node = move.Inputs[0];
        }
        return node;
    }

    private bool CanCoalesce(TempVariableNode copy, TempVariableNode source, IStatement copyStatement,
        List<IStatement> statements, VariableLiveness liveness)
    {
        if (copy.IsInteger != source.IsInteger || copy.IsUnsigned != source.IsUnsigned
            || copy.IsBits != source.IsBits || copy.IsDouble != source.IsDouble
            || copy.IsBool != source.IsBool || copy.IsHalf != source.IsHalf
            || IsLoopClauseVariable(copy) || IsLoopClauseVariable(source)
            // An atomic's out variable is declared at the call: merged with a
            // loop's `cur = prev`, the call redeclared cur inside the loop.
            || statements.Any(s => s is AtomicStatement atomic
                && (ReferenceEquals(atomic.Original, copy) || ReferenceEquals(atomic.Original, source))))
        {
            return false;
        }
        foreach (IStatement statement in statements)
        {
            HashSet<TempVariableNode> writes = VariableLiveness.Writes(statement);
            if (writes.Contains(copy) && !ReferenceEquals(statement, copyStatement)
                && liveness.MayBeLiveAfter(source, statement))
            {
                return false;
            }
            // A copy back assigns the same value either way.
            if (writes.Contains(source) && !CopiesBack(statement, source, copy)
                && liveness.MayBeLiveAfter(copy, statement))
            {
                return false;
            }
        }
        return true;
    }

    private static bool CopiesBack(IStatement statement, TempVariableNode variable, TempVariableNode from)
    {
        return statement.Outputs.Values.OfType<TempAssignmentNode>()
            .Where(assignment => ReferenceEquals(assignment.TempVariable, variable))
            .All(assignment => ReferenceEquals(Unmoved(assignment.Value), from));
    }

    private bool IsLoopClauseVariable(TempVariableNode variable)
    {
        bool found = false;
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is LoopStatement loop
                && (ReferenceEquals(loop.Initializer?.TempVariable, variable)
                    || ReferenceEquals(loop.Increment?.TempVariable, variable)))
            {
                found = true;
            }
        });
        return found;
    }

    // After coalescing, the copies are `t = t`; drop them.
    private void RemoveSelfCopies(TempVariableNode variable)
    {
        new StatementVisitor(_statements).Visit(statement =>
        {
            foreach (var output in statement.Outputs.ToList())
            {
                if (output.Value is TempAssignmentNode assignment
                    && ReferenceEquals(assignment.TempVariable, variable)
                    && ReferenceEquals(Unmoved(assignment.Value), variable))
                {
                    assignment.Replace(variable);
                    ReplaceAnyAssignment(output.Key, assignment, variable);
                }
            }
        });
    }

    /// <summary>
    /// Replaces a join phi whose leaves are all one variable with that variable.
    /// Catches phis of phis (nested joins) that branch lowering leaves behind.
    /// </summary>
    private void LowerResolvedPhis()
    {
        new StatementVisitor(_statements).Visit(statement =>
        {
            foreach (var entry in statement.Outputs.Concat(statement.Inputs)
                .Where(e => e.Value is PhiNode { IsLoopHeader: false })
                .Distinct()
                .ToList())
            {
                TempVariableNode variable = GetExistingVariable(entry.Value);
                if (variable == null)
                {
                    continue;
                }
                foreach (var reader in entry.Value.Outputs.ToList())
                {
                    for (int j = 0; j < reader.Inputs.Count; j++)
                    {
                        if (ReferenceEquals(reader.Inputs[j], entry.Value))
                        {
                            reader.Inputs[j] = variable;
                        }
                    }
                    variable.Outputs.Add(reader);
                }
                entry.Value.Outputs.Clear();
                ReplaceInStatementNodes(entry.Value, variable);
            }
        });
    }

    private void RemoveUnusedAssignmentInputOutput()
    {
        new StatementVisitor(_statements).Visit(statement =>
        {
            var inputsToRemove = statement.Inputs
                .Where(i => !(i.Key.RegisterKey.IsTempRegister || i.Key.RegisterKey.IsOutput))
                .ToList();
            foreach (var output in inputsToRemove)
            {
                statement.Inputs.Remove(output.Key);
            }

            var outputsToRemove = statement.Outputs
                .Where(o => !(o.Key.RegisterKey.IsTempRegister || o.Key.RegisterKey.IsOutput))
                .ToList();
            foreach (var output in outputsToRemove)
            {
                statement.Outputs.Remove(output.Key);
                // An unread mova would keep its operand alive as a variable.
                if (output.Key.RegisterKey is D3D9RegisterKey { Type: RegisterType.Addr }
                    && output.Value is MoveOperation && output.Value.Outputs.Count == 0)
                {
                    output.Value.Remove();
                }
            }

            // Unread phis, from any block, keep inputs alive for nothing. A phi a
            // statement holds (e.g. a store) is still read, though not via the graph.
            outputsToRemove = statement.Outputs
                .Where(o => o.Value is PhiNode && o.Value.Outputs.Count == 0)
                .Where(o => !IsRenderedByAnyStatement(o.Value))
                .Where(o => !_returnedOutputJoins.ContainsKey((PhiNode)o.Value))
                .ToList();
            foreach (var output in outputsToRemove)
            {
                statement.Outputs.Remove(output.Key);
                output.Value.Remove();
            }
        });
    }

    private bool IsUnreadLoopStart(HlslTreeNode reader, HlslTreeNode value)
    {
        return reader is PhiNode { IsLoopHeader: true, Outputs.Count: 0 } phi
            && ReferenceEquals(phi.PreLoopValue, value)
            && !IsRenderedByAnyStatement(phi);
    }

    private void RemoveUnusedAssignments(IList<IStatement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            RemoveUnusedAssignments(statements, i);
        }
    }

    private void RemoveUnusedAssignments(IList<IStatement> statements, int i)
    {
        if (statements[i] is AssignmentStatement assignment)
        {
            // Only computed values: a carried one belongs to the statement that
            // computed it, and judging it here would inline loop invariants.
            var assignmentOutputs = assignment.Outputs
                .Where(o => o.Key.RegisterKey.IsTempRegister)
                .Where(o => !(assignment.Inputs.TryGetValue(o.Key, out var input) && ReferenceEquals(input, o.Value)))
                // Read by the caller (fcall), invisible here.
                .Where(o => !_liveOut.Contains(o.Key.RegisterKey))
                .ToDictionary();
            // By register, not component: of a load's four components only the one a
            // condition reads looks shared.
            HashSet<RegisterComponentKey> sharedRegisters = SharedWithHoldingStatement(assignmentOutputs);
            foreach (var assignmentOutput in assignmentOutputs)
            {
                var assignmentNode = assignmentOutput.Value;

                // A constant or input read is never worth a variable, unless a phi
                // reads it (a loop's start value) or a statement holds it (e.g. a
                // flag a break carries out of the loop).
                if (IsFreeToRead(assignmentNode) && assignmentNode.Outputs.All(v => v is not PhiNode)
                    && FindHoldingStatements(assignmentNode).Length == 0)
                {
                    RemoveAnyAssignment(assignmentNode);
                    continue;
                }

                // Read only within this statement; a phi reader means it leaves,
                // unless it is a loop phi nothing reads: the loop writes the register
                // before reading it, so fxc only reused it.
                if (assignmentNode.Outputs.All(v => IsUnreadLoopStart(v, assignmentNode)
                    || (v is not PhiNode && v.IsInputOf(assignment.Outputs.Values))))
                {
                    // A local array store may inline the value only if it is the very
                    // next statement; a store between could change what it reads.
                    IStatement[] holders = FindHoldingStatements(assignmentNode);
                    if ((holders.Length == 0 && !sharedRegisters.Contains(assignmentOutput.Key))
                        || (holders.Length == 1 && i < statements.Count - 1
                            && ReferenceEquals(holders[0], statements[i + 1])
                            && statements[i + 1] is IndexableTempStoreStatement))
                    {
                        RemoveAnyAssignment(assignmentNode);
                        continue;
                    }
                }

                // Check if assignment output goes only into the next statement
                if (i < statements.Count - 1)
                {
                    IStatement nextStatement = statements[i + 1];
                    if (nextStatement is ClipStatement clip)
                    {
                        // Only when the clip is its sole reader.
                        if (assignmentNode.IsInputOf(clip.Values)
                            && assignmentNode.Outputs.All(o => o is not PhiNode && o.IsInputOf(clip.Values)))
                        {
                            assignment.Outputs.Remove(assignmentOutput.Key);
                            clip.Inputs.Remove(assignmentOutput.Key);
                        }
                    }
                    else if (nextStatement is IfStatement ifStatement)
                    {
                        // The condition inlines a value only it reads. A phi reader is
                        // the value leaving: a loop's header phi is upstream of the
                        // condition too, through the loop, so IsInputOf counts it in.
                        if (assignmentNode.IsInputOf(ifStatement.Comparison)
                            && assignmentNode.Outputs.All(o => o is not PhiNode && o.IsInputOf(ifStatement.Comparison))
                            && !sharedRegisters.Contains(assignmentOutput.Key))
                        {
                            assignment.Outputs.Remove(assignmentOutput.Key);
                            ifStatement.Inputs.Remove(assignmentOutput.Key);
                            if (ifStatement.Outputs.TryGetValue(assignmentOutput.Key, out var ifOutput)
                                && ifOutput == assignmentNode)
                            {
                                ifStatement.Outputs.Remove(assignmentOutput.Key);
                            }
                        }
                    }
                }
            }
        }
        else if (statements[i] is IfStatement ifStatement)
        {
            RemoveUnusedAssignments(ifStatement.TrueBody);
            if (ifStatement.FalseBody != null)
            {
                RemoveUnusedAssignments(ifStatement.FalseBody);
            }
        }
        else if (statements[i] is LoopStatement loopStatement)
        {
            RemoveUnusedAssignments(loopStatement.Body);
        }
        else if (statements[i] is SwitchStatement switchStatement)
        {
            foreach (SwitchCase switchCase in switchStatement.Cases)
            {
                RemoveUnusedAssignments(switchCase.Body);
            }
        }
    }

    private void InsertTempVariableAssignments(IList<IStatement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            InsertTempVariableAssignments(statements, i);
        }
    }

    private void InsertTempVariableAssignments(IList<IStatement> statements, int i)
    {
        IStatement statement = statements[i];

        if (statement is AssignmentStatement)
        {
            var newAssignments = statement.Outputs
                .Where(o => o.Key.RegisterKey.IsTempRegister)
                .Where(o => !statement.Inputs.ContainsKey(o.Key) || statement.Inputs[o.Key] != statement.Outputs[o.Key])
                .ToList();
            // Which value feeds which, taken before lowering makes new and old value
            // the same name.
            var feeds = new Dictionary<RegisterComponentKey, List<RegisterComponentKey>>();
            // A reader of a stepped value that also reads the value before the step
            // (`iadd r0.y, r0.y, 1` then `movc r0.x, (cb[r0.y_old] < x), r0.y, r0.x`)
            // cannot be ordered: lowering names both values the same variable. It
            // keeps the step's expression instead, all its reads then of the old value.
            var mixedReaders = new Dictionary<RegisterComponentKey, List<HlslTreeNode>>();
            foreach (var reader in newAssignments)
            {
                feeds[reader.Key] = [];
                foreach (var fed in newAssignments)
                {
                    if (ReferenceEquals(fed.Value, reader.Value) || !fed.Value.IsInputOf(reader.Value))
                    {
                        continue;
                    }
                    // The old value is what the step reads of its own register: its
                    // input node, or - loop-carried, where a header phi was lowered
                    // already - the variable the step reads. A step into a register
                    // it does not also read from is no step, and conflicts with nothing.
                    statement.Inputs.TryGetValue(fed.Key, out HlslTreeNode oldInput);
                    List<HlslTreeNode> oldValues = [];
                    if (oldInput != null && oldInput.IsInputOf(fed.Value))
                    {
                        oldValues.Add(oldInput);
                    }
                    if (oldInput is PhiNode { IsLoopHeader: true }
                        || (oldInput != null && GetExistingVariable(oldInput) is TempVariableNode))
                    {
                        TempVariableNode own = GetExistingVariable(oldInput);
                        oldValues.AddRange(fed.Value.Inputs.OfType<TempVariableNode>()
                            .Where(v => own == null || ReferenceEquals(v, own)));
                    }
                    if (oldValues.Any(old => ReadsAvoiding(reader.Value, old, fed.Value)))
                    {
                        if (!mixedReaders.TryGetValue(fed.Key, out List<HlslTreeNode> readers))
                        {
                            mixedReaders[fed.Key] = readers = [];
                        }
                        readers.Add(reader.Value);
                        continue;
                    }
                    feeds[reader.Key].Add(fed.Key);
                }
            }
            // A statement holding a mixed reader by value (a store of
            // `and(ult(r0.y, x), 1) + umin(x, r0.y)`) is one too; no register keeps it.
            foreach (var fed in newAssignments)
            {
                statement.Inputs.TryGetValue(fed.Key, out HlslTreeNode oldInput);
                TempVariableNode own = oldInput is TempAssignmentNode assigned
                    ? assigned.TempVariable
                    : GetExistingVariable(oldInput);
                HlslTreeNode old = own != null && Reads(fed.Value, own) ? own
                    : oldInput != null && oldInput.IsInputOf(fed.Value) ? oldInput
                    : null;
                if (old == null)
                {
                    continue;
                }
                new StatementVisitor(_statements).Visit(holder =>
                {
                    foreach (HlslTreeNode root in holder.HeldNodes)
                    {
                        if (root != null && !ReferenceEquals(root, fed.Value)
                            && fed.Value.IsInputOf(root) && ReadsAvoiding(root, old, fed.Value))
                        {
                            if (!mixedReaders.TryGetValue(fed.Key, out List<HlslTreeNode> readers))
                            {
                                mixedReaders[fed.Key] = readers = [];
                            }
                            readers.Add(root);
                        }
                    }
                });
            }
            // Likewise for output registers. An output whose value is the temp's own
            // is excluded; the sort's overwrite rule handles it.
            var outputFeeds = new Dictionary<RegisterComponentKey, List<RegisterComponentKey>>();
            foreach (var output in statement.Outputs.Where(o => !o.Key.RegisterKey.IsTempRegister))
            {
                outputFeeds[output.Key] = [.. newAssignments
                    .Where(fed => !ReferenceEquals(fed.Value, output.Value)
                        && fed.Value.IsInputOf(output.Value))
                    .Select(fed => fed.Key)];
            }
            HashSet<RegisterComponentKey> sharedRegisters = SharedWithHoldingStatement(newAssignments);
            var assignmentByKey = new Dictionary<RegisterComponentKey, TempAssignmentNode>();
            foreach (var newAssignment in newAssignments)
            {
                HlslTreeNode tempValue = newAssignment.Value;

                // Read outside this statement, including by a holding statement.
                bool doesOutputExitStatement = tempValue.Outputs.Any(v => !v.IsInputOf(statement.Outputs.Values))
                    || FindHoldingStatements(tempValue).Length != 0
                    || sharedRegisters.Contains(newAssignment.Key);
                statement.Inputs.TryGetValue(newAssignment.Key, out var inputAssignment);
                var tempInputAssignment = inputAssignment as TempAssignmentNode;
                TempVariableNode tempInputVariable = GetExistingVariable(inputAssignment);
                // A value that is already a variable (an atomic's out parameter) needs
                // no copy, unless the register had a variable or a phi reads it.
                if (tempValue is TempVariableNode && tempInputAssignment == null && tempInputVariable == null
                    && !tempValue.Outputs.Any(usage => usage is PhiNode))
                {
                    continue;
                }
                // Read only by mixed readers, which keep the expression: assigning it
                // would overwrite the old value they still read.
                if (mixedReaders.TryGetValue(newAssignment.Key, out List<HlslTreeNode> mixedRoots)
                    && tempValue.Outputs.Count != 0
                    && tempValue.Outputs.All(usage => usage is not PhiNode
                        && mixedRoots.Any(root => ReferenceEquals(root, usage) || usage.IsInputOf(root)))
                    && !IsRenderedByAnyStatement(tempValue))
                {
                    statement.Outputs.Remove(newAssignment.Key);
                    continue;
                }
                if (doesOutputExitStatement || tempInputAssignment != null)
                {
                    List<HlslTreeNode> tempUsages = tempValue.Outputs.ToList();
                    // Before the readers move to the variable: they are what types it.
                    // Moved only into outputs, the signature types it before the register.
                    // A double instruction's write is a double, its literal too (`dmov
                    // r0.xy, d(0)` before a loop read as an int 0).
                    bool? isIntegerValue = _doubleValues.Contains(tempValue)
                        ? false
                        : ValueTypes.IsIntegerValue(tempValue) ?? OutputTypeHolding(tempValue);
                    bool? isUnsignedValue = ValueTypes.IsUnsignedValue(tempValue);
                    bool isInteger = isIntegerValue
                        ?? (_integerOperandAnalysis?.IsIntegerRegister(newAssignment.Key) == true);
                    bool isBits = ValueTypes.IsBitsVariable(tempValue, isInteger);
                    tempValue.Outputs.Clear();
                    TempVariableNode existing = tempInputAssignment?.TempVariable ?? tempInputVariable;
                    // A reused register is a new variable when the type differs, or
                    // when the value does not read the old one (`t = t + x` does),
                    // unless it feeds a phi over the existing variable.
                    if (existing != null
                        && ((isIntegerValue != null && existing.IsInteger != isIntegerValue
                                && !tempUsages.Any(u => u is PhiNode))
                            // A select on a condition overwriting it holds a value.
                            || (existing.IsBool && !HoldsOnlyACondition(tempValue, tempUsages))
                            || (!Reads(tempValue, existing)
                                && !tempUsages.Any(u => u is PhiNode phi && Merges(phi, existing)))))
                    {
                        existing = null;
                        tempInputAssignment = null;
                        tempInputVariable = null;
                    }
                    TempVariableNode tempVariable = existing
                        ?? new TempVariableNode
                        {
                            IsInteger = isInteger,
                            IsBits = isBits,
                            IsUnsigned = isInteger && !isBits && isUnsignedValue == true,
                            // Only the writing instruction knows a double.
                            IsDouble = !isInteger && _doubleValues.Contains(tempValue),
                            IsBool = HoldsOnlyACondition(tempValue, tempUsages),
                        };
                    var tempAssignment = new TempAssignmentNode(tempVariable, tempValue);
                    // Only the pre-loop value declares; other phi feeds reassign.
                    bool declaresLoopVariable = tempUsages.Count != 0
                        && tempUsages.All(u => u is PhiNode phi
                            && phi.IsLoopHeader
                            && ReferenceEquals(phi.PreLoopValue, tempValue));
                    // No readers at all (held only) is a declaration, not "all phis".
                    if ((tempUsages.Count != 0 && tempUsages.All(u => u is PhiNode) && !declaresLoopVariable)
                        || tempInputAssignment != null
                        || tempInputVariable != null)
                    {
                        tempAssignment.IsReassignment = true;
                    }
                    foreach (var tempUsage in tempUsages)
                    {
                        if (tempUsage is PhiNode)
                        {
                            foreach (var output in tempUsage.Outputs)
                            {
                                for (int j = 0; j < output.Inputs.Count; j++)
                                {
                                    if (output.Inputs[j] == tempUsage)
                                    {
                                        output.Inputs[j] = tempVariable;
                                    }
                                }
                                tempVariable.Outputs.Add(output);
                            }
                            // Else a second visit of the phi rewires readers again.
                            tempUsage.Outputs.Clear();
                            ReplaceInStatementNodes(tempUsage, tempVariable);
                        }
                        // A mixed reader keeps the expression (see mixedReaders).
                        else if (mixedReaders.TryGetValue(newAssignment.Key, out List<HlslTreeNode> mixed)
                            && mixed.Any(root => ReferenceEquals(root, tempUsage) || tempUsage.IsInputOf(root)))
                        {
                            tempValue.Outputs.Add(tempUsage);
                            continue;
                        }
                        // Kept so later rewiring (branch unification) finds the phi too.
                        tempVariable.Outputs.Add(tempUsage);
                        int index = tempUsage.Inputs.IndexOf(tempValue);
                        tempUsage.Inputs[index] = tempVariable;
                    }
                    ReplaceInStatementNodes(tempValue, tempVariable);
                    ReplaceAnyAssignment(newAssignment.Key, tempValue, tempAssignment);
                    assignmentByKey[newAssignment.Key] = tempAssignment;
                }
            }

            // Recorded by assignment, not variable: unification re-points variables.
            foreach ((RegisterComponentKey key, TempAssignmentNode assignment) in assignmentByKey)
            {
                foreach (RegisterComponentKey fed in feeds[key])
                {
                    if (assignmentByKey.TryGetValue(fed, out TempAssignmentNode feeder))
                    {
                        assignment.DependsOnNewValueOf.Add(feeder);
                    }
                }
            }
            var assignmentStatement = (AssignmentStatement)statement;
            foreach ((RegisterComponentKey key, List<RegisterComponentKey> fedBy) in outputFeeds)
            {
                List<TempAssignmentNode> feeders = [.. fedBy
                    .Where(assignmentByKey.ContainsKey)
                    .Select(fed => assignmentByKey[fed])];
                if (feeders.Count != 0)
                {
                    assignmentStatement.OutputDependsOnNewValueOf[key] = feeders;
                }
            }
        }
        else if (statement is IfStatement ifStatement)
        {
            InsertTempVariableAssignments(ifStatement.TrueBody);
            if (ifStatement.FalseBody != null)
            {
                InsertTempVariableAssignments(ifStatement.FalseBody);
            }
            UnifyBranchVariables(ifStatement);
        }
        else if (statement is SwitchStatement switchStatement)
        {
            foreach (SwitchCase switchCase in switchStatement.Cases)
            {
                InsertTempVariableAssignments(switchCase.Body);
            }
            UnifyCaseVariables(switchStatement);
        }
        else if (statement is LoopStatement loopStatement)
        {
            InsertTempVariableAssignments(loopStatement.Body);

            if (i >= 1 && statements[i - 1] is AssignmentStatement preLoopStatement)
            {
                UseLoopVariablesInBody(loopStatement, preLoopStatement);
            }
        }
    }

    // The variable an if hands out, directly or via its join phi, so reassigning it
    // does not declare a second one.
    private static TempVariableNode GetExistingVariable(HlslTreeNode inputAssignment)
    {
        if (inputAssignment is TempVariableNode variable)
        {
            return variable;
        }
        // Not loop header phis. A phi of phis is one variable when every leaf is.
        if (inputAssignment is PhiNode phi && !phi.IsLoopHeader && phi.Inputs.Count != 0)
        {
            TempVariableNode merged = null;
            foreach (HlslTreeNode input in phi.Inputs)
            {
                TempVariableNode leaf = GetExistingVariable(input);
                if (leaf == null || (merged != null && !ReferenceEquals(leaf, merged)))
                {
                    return null;
                }
                merged = leaf;
            }
            return merged;
        }
        return null;
    }

    /// <summary>
    /// Puts both branches' assignments of a register on one variable declared above
    /// the if; the first branch's variable wins and all become reassignments.
    /// </summary>
    private void UnifyBranchVariables(IfStatement ifStatement)
    {
        var variableByRegister = new Dictionary<RegisterComponentKey, TempVariableNode>();

        foreach (IList<IStatement> body in new[] { ifStatement.TrueBody, ifStatement.FalseBody })
        {
            if (body == null || body.Count == 0)
            {
                continue;
            }
            foreach (var output in body.Last().Outputs)
            {
                // Either an assignment or a nested if's merged variable.
                var assignment = output.Value as TempAssignmentNode;
                TempVariableNode branchVariable =
                    assignment?.TempVariable ?? output.Value as TempVariableNode;
                if (branchVariable == null)
                {
                    continue;
                }
                // Carried through unchanged: not the branch's to assign.
                if (ifStatement.Inputs.TryGetValue(output.Key, out HlslTreeNode entryValue)
                    && ReferenceEquals(entryValue, output.Value))
                {
                    continue;
                }
                if (variableByRegister.TryGetValue(output.Key, out TempVariableNode variable))
                {
                    // May already be shared (two ifs in a row on one register).
                    if (assignment == null)
                    {
                        // A nested if's variable: its assignments are deeper in the body.
                        ReplaceTempVariable(branchVariable, variable);
                    }
                    else if (!ReferenceEquals(branchVariable, variable))
                    {
                        branchVariable.Replace(variable);
                        // Replace does not reach held references.
                        ReplaceInStatementNodes(branchVariable, variable);
                        assignment.TempVariable = variable;
                    }
                }
                else
                {
                    variable = branchVariable;
                    variableByRegister.Add(output.Key, variable);
                }
                if (assignment != null)
                {
                    assignment.IsReassignment = true;
                }
                ifStatement.Outputs[output.Key] = variable;
            }
        }
    }

    /// <summary>
    /// <see cref="UnifyBranchVariables"/> for switch cases.
    /// </summary>
    private void UnifyCaseVariables(SwitchStatement switchStatement)
    {
        var variableByRegister = new Dictionary<RegisterComponentKey, TempVariableNode>();

        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            if (switchCase.Body.Count == 0)
            {
                continue;
            }
            foreach (var caseOutput in switchCase.Body.Last().Outputs)
            {
                if (caseOutput.Value is not TempAssignmentNode caseAssignment)
                {
                    continue;
                }
                if (variableByRegister.TryGetValue(caseOutput.Key, out var sharedVariable))
                {
                    caseAssignment.TempVariable = sharedVariable;
                }
                else
                {
                    variableByRegister[caseOutput.Key] = caseAssignment.TempVariable;
                }
                // The declaration is hoisted above the switch, so every case reassigns.
                caseAssignment.IsReassignment = true;
            }
        }

        // A case ending in a nested if/switch carries out a join phi; its variable is
        // what the case assigns. Done after the plain assignments so that one of
        // theirs, which the enclosing scope reads, owns the register.
        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            if (switchCase.Body.Count == 0)
            {
                continue;
            }
            foreach (var caseOutput in switchCase.Body.Last().Outputs)
            {
                if (caseOutput.Value is not PhiNode
                    || VariableAssignedInBody(switchCase.Body, caseOutput.Key)
                        is not TempVariableNode nested)
                {
                    continue;
                }
                if (variableByRegister.TryGetValue(caseOutput.Key, out var owner))
                {
                    Reassign(switchCase.Body, nested, owner);
                }
                else
                {
                    variableByRegister[caseOutput.Key] = nested;
                }
            }
        }

        // A break nested in an if carries that if's join variable out of the case
        // even when the case ends with a plain assignment after the if. It must
        // share the case's variable, or the enclosing scope reads an unset one.
        foreach (SwitchCase switchCase in switchStatement.Cases)
        {
            foreach (IfStatement bodyIf in switchCase.Body.OfType<IfStatement>())
            {
                foreach (var ifOutput in bodyIf.Outputs.ToList())
                {
                    if (ifOutput.Value is not TempVariableNode carried
                        || !variableByRegister.TryGetValue(ifOutput.Key, out var owner)
                        || ReferenceEquals(carried, owner)
                        || (bodyIf.Inputs.TryGetValue(ifOutput.Key, out HlslTreeNode entryValue)
                            && ReferenceEquals(entryValue, carried)))
                    {
                        continue;
                    }
                    Reassign(switchCase.Body, carried, owner);
                }
            }
        }

        // The first case's variable may have taken the register's type for a literal
        // (`r0.x` held the selector, an int, before `mov r0.x, l(1.0)`); the
        // values the cases assign say what the variable holds.
        foreach (var entry in variableByRegister)
        {
            List<bool?> kinds = [.. switchStatement.Cases
                .Where(c => c.Body.Count != 0)
                .Select(c => c.Body.Last().Outputs.TryGetValue(entry.Key, out HlslTreeNode value)
                        && value is TempAssignmentNode assigned
                    ? ValueTypes.IsIntegerValue(assigned.Value)
                    : null)];
            if (entry.Value.IsInteger && kinds.Contains(false) && !kinds.Contains(true))
            {
                entry.Value.IsInteger = false;
                entry.Value.IsBits = false;
                entry.Value.IsUnsigned = false;
            }
        }

        foreach (var entry in variableByRegister)
        {
            switchStatement.Outputs[entry.Key] = entry.Value;
        }
    }

    /// <summary>
    /// The variable the case last put the register in, looking back past the break.
    /// </summary>
    private static TempVariableNode VariableAssignedInBody(
        IList<IStatement> body, RegisterComponentKey key)
    {
        for (int i = body.Count - 1; i >= 0; i--)
        {
            if (!body[i].Outputs.TryGetValue(key, out HlslTreeNode output))
            {
                continue;
            }
            if (output is TempVariableNode variable)
            {
                return variable;
            }
            if (output is TempAssignmentNode assignment)
            {
                return assignment.TempVariable;
            }
        }
        return null;
    }

    /// <summary>
    /// Points every assignment and read of one variable in the body at another.
    /// </summary>
    private void Reassign(
        IList<IStatement> body, TempVariableNode from, TempVariableNode to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        // Graph reads (e.g. a nested loop's backedge) and held reads (a store's
        // operands) as well as assignments, or they name an undeclared variable.
        from.Replace(to);
        ReplaceInStatementNodes(from, to);

        new StatementVisitor(body).Visit(statement =>
        {
            foreach (var output in statement.Outputs.ToList())
            {
                if (output.Value is TempAssignmentNode assignment
                    && ReferenceEquals(assignment.TempVariable, from))
                {
                    assignment.TempVariable = to;
                    assignment.IsReassignment = true;
                }
                else if (ReferenceEquals(output.Value, from))
                {
                    statement.Outputs[output.Key] = to;
                }
            }
            foreach (var input in statement.Inputs
                .Where(i => ReferenceEquals(i.Value, from)).ToList())
            {
                statement.Inputs[input.Key] = to;
            }
        });
    }

    private void SetReturnStatement(IList<IStatement> statements)
    {
        IStatement lastStatement = statements.Last();

        if (lastStatement is ReturnStatement)
        {
            return;
        }

        // Geometry and compute shaders return void.
        if (!_hasReturnValue)
        {
            return;
        }

        if (lastStatement is AssignmentStatement)
        {
            statements[statements.Count - 1] =
                new ReturnStatement(lastStatement.Inputs, lastStatement.Outputs);
            return;
        }
        if (lastStatement is IfStatement ifStatement)
        {
            // One return after the if where possible: a return per branch costs fxc
            // a ret instruction apiece.
            if (ifStatement.FalseBody != null && !_hasOutputStruct
                && !ReturnOnceAfter(statements, ifStatement))
            {
                SetReturnStatement(ifStatement.TrueBody);
                SetReturnStatement(ifStatement.FalseBody);
            }
            else
            {
                statements.Add(new ReturnStatement(ifStatement.Outputs));
            }
            return;
        }
        if (lastStatement is LoopStatement || lastStatement is ClipStatement
            || lastStatement is DiscardStatement
            || lastStatement is BreakStatement || lastStatement is ContinueStatement
            || lastStatement is SwitchStatement
            // A side-effect statement can still precede a returned value.
            || lastStatement is AppendStatement
            || lastStatement is StoreStructuredStatement
            || lastStatement is StoreTypedStatement
            || lastStatement is BufferAppendStatement
            || lastStatement is AtomicStatement
            || lastStatement is IndexableTempStoreStatement
            || lastStatement is RestartStripStatement
            || lastStatement is SyncStatement)
        {
            statements.Add(new ReturnStatement(lastStatement.Outputs));
            return;
        }
        throw new NotImplementedException(lastStatement.GetType().Name);
    }

    /// <summary>
    /// For an if/else whose branches both end by writing the whole single output,
    /// gives the output a variable both assign so the caller returns once after the
    /// if, matching the bytecode's single ret.
    /// </summary>
    private bool ReturnOnceAfter(IList<IStatement> statements, IfStatement ifStatement)
    {
        if (FallThroughPaths(ifStatement).Count != 2)
        {
            return false;
        }
        // An output assignment holds every active lane.
        AssignmentStatement written = null;
        new StatementVisitor(ifStatement.TrueBody).Visit(statement =>
        {
            if (statement is AssignmentStatement assignment && assignment.Outputs.Keys
                .Any(key => key.RegisterKey.IsOutput && !IsCarried(assignment, key)))
            {
                written = assignment;
            }
        });
        List<RegisterComponentKey> outputs = [.. (written?.Outputs.Keys ?? [])
            .Where(key => key.RegisterKey.IsOutput)
            .OrderBy(key => key.ComponentIndex)];
        // SM4+ only; a ps_3_0 return per branch costs nothing.
        if (outputs.Count == 0
            || outputs[0].RegisterKey is not D3D10RegisterKey
            || outputs.Select(key => key.RegisterKey).Distinct().Count() != 1
            // Written in every path, not carried in from before the if.
            || !outputs.All(key => LastWrite(ifStatement.TrueBody, key) != null
                && LastWrite(ifStatement.FalseBody, key) != null))
        {
            return false;
        }
        foreach (RegisterComponentKey key in outputs)
        {
            var variable = new TempVariableNode
            {
                IsInteger = _integerOperandAnalysis?.IsIntegerOutputSignature(key) == true,
            };
            AssignJoinedOutput(ifStatement, key, variable);
            ifStatement.Outputs[key] = variable;
        }
        return true;
    }

    // The branches that reach the end of the if (an absent else is an empty one).
    private static List<IList<IStatement>> FallThroughPaths(IfStatement ifStatement)
    {
        return [.. new[] { ifStatement.TrueBody, ifStatement.FalseBody ?? [] }
            .Where(body => body.Count == 0 || body[^1] is not
                (ReturnStatement or DiscardStatement or BreakStatement or ContinueStatement))];
    }

    // The body's last write of the lane when every path through it writes the lane:
    // an assignment, or an if whose branches each write it. Null otherwise.
    private static IStatement LastWrite(IList<IStatement> body, RegisterComponentKey key)
    {
        for (int i = body.Count - 1; i >= 0; i--)
        {
            IStatement statement = body[i];
            if (statement is AssignmentStatement assignment)
            {
                if (assignment.Outputs.ContainsKey(key) && !IsCarried(assignment, key))
                {
                    return assignment;
                }
            }
            else if (statement is IfStatement nested)
            {
                List<IList<IStatement>> paths = FallThroughPaths(nested);
                int writing = paths.Count(path => Writes(path, key));
                if (writing != 0)
                {
                    return writing == paths.Count && paths.All(path => LastWrite(path, key) != null)
                        ? nested : null;
                }
            }
            else if (Writes([statement], key))
            {
                return null;
            }
        }
        return null;
    }

    private static bool Writes(IList<IStatement> statements, RegisterComponentKey key)
    {
        bool writes = false;
        new StatementVisitor(statements).Visit(statement => writes |= statement is AssignmentStatement
            && statement.Outputs.ContainsKey(key) && !IsCarried(statement, key));
        return writes;
    }

    // An if writing some output lanes, then later statements and the return: the
    // single-value return reads those lanes from the if's join. An if at the end
    // returns in its branches instead (or via ReturnOnceAfter), but still returns
    // lanes earlier ifs joined. A ret inside a block reads them the same way.
    private void FindReturnedOutputJoins()
    {
        if (!_hasReturnValue || _hasOutputStruct || _statements.Count == 0)
        {
            return;
        }
        List<IStatement> returns = [];
        if (_statements[^1] is not ReturnStatement)
        {
            returns.Add(_statements[^1]);
        }
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is ReturnStatement)
            {
                returns.Add(statement);
            }
        });
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is not IfStatement ifStatement || ReferenceEquals(statement, _statements[^1]))
            {
                return;
            }
            foreach (var output in ifStatement.Outputs)
            {
                if (output.Key.RegisterKey.IsOutput
                    && output.Value is PhiNode { IsLoopHeader: false } join
                    && !(ifStatement.Inputs.TryGetValue(output.Key, out HlslTreeNode entry)
                        && ReferenceEquals(entry, join))
                    && returns.Any(r => r.Outputs.TryGetValue(output.Key, out HlslTreeNode returned)
                        && ReferenceEquals(returned, join)))
                {
                    _returnedOutputJoins[join] = ifStatement;
                }
            }
        });
    }

    // Each joined lane becomes a variable every branch assigns.
    private void LowerReturnedOutputJoins()
    {
        foreach ((PhiNode join, IfStatement ifStatement) in _returnedOutputJoins)
        {
            RegisterComponentKey key = ifStatement.Outputs.First(o => ReferenceEquals(o.Value, join)).Key;
            var variable = new TempVariableNode
            {
                IsInteger = _integerOperandAnalysis?.IsIntegerOutputSignature(key) == true,
            };
            AssignJoinedOutput(ifStatement, key, variable);
            new StatementVisitor(_statements).Visit(statement =>
            {
                foreach (var map in new[] { statement.Outputs, statement.Inputs })
                {
                    if (map.TryGetValue(key, out HlslTreeNode held) && ReferenceEquals(held, join))
                    {
                        map[key] = variable;
                    }
                }
            });
            ReplaceInStatementNodes(join, variable);
        }
    }

    private static void AssignJoinedOutput(IfStatement ifStatement, RegisterComponentKey key,
        TempVariableNode variable)
    {
        foreach (IList<IStatement> body in FallThroughPaths(ifStatement))
        {
            IStatement write = LastWrite(body, key);
            if (write is IfStatement nested)
            {
                AssignJoinedOutput(nested, key, variable);
            }
            else if (write != null)
            {
                write.Outputs[key] = new TempAssignmentNode(variable, write.Outputs[key]) { IsReassignment = true };
            }
            else
            {
                // A lane one branch leaves as it was before the if.
                throw new NotImplementedException("An output lane assigned in only some branches of an if.");
            }
        }
    }

    private static bool IsCarried(IStatement statement, RegisterComponentKey key)
    {
        return statement.Inputs.TryGetValue(key, out HlslTreeNode input)
            && ReferenceEquals(input, statement.Outputs[key]);
    }

    /// <summary>
    /// A register assigned before the loop reuses that variable wherever the body
    /// assigns it, including inside branches and before a <c>continue</c>.
    /// </summary>
    private void UseLoopVariablesInBody(LoopStatement loopStatement, AssignmentStatement preLoopStatement)
    {
        new StatementVisitor(loopStatement.Body).Visit(bodyStatement =>
        {
            foreach (var bodyOutput in bodyStatement.Outputs.ToList())
            {
                if (!preLoopStatement.Outputs.TryGetValue(bodyOutput.Key, out var preLoopValue)
                    || ((preLoopValue as TempAssignmentNode)?.TempVariable ?? preLoopValue as TempVariableNode)
                        is not TempVariableNode loopVariable)
                {
                    continue;
                }

                if (bodyOutput.Value is TempAssignmentNode bodyAssignment)
                {
                    // Readers too, not just the assignment's property.
                    ReplaceLoopVariable(bodyAssignment.TempVariable, loopVariable, loopStatement, preLoopStatement);
                }
                else if (bodyOutput.Value is TempVariableNode joinVariable)
                {
                    // A branch join's own variable is the loop-carried one.
                    ReplaceLoopVariable(joinVariable, loopVariable, loopStatement, preLoopStatement);
                }
                else if (bodyOutput.Value is PhiNode joinPhi && !joinPhi.IsLoopHeader)
                {
                    // Unlowered join phi: its operands are the shared variable.
                    foreach (var branchVariable in joinPhi.Inputs.OfType<TempVariableNode>().ToList())
                    {
                        ReplaceTempVariable(branchVariable, loopVariable);
                    }
                }
            }
        });
    }

    // Merges, for SplitUncarriedLoopValues to revisit.
    private readonly List<(LoopStatement Loop, IStatement PreLoop, TempVariableNode Carried,
        TempVariableNode Own, List<TempAssignmentNode> Assignments)> _loopMerges = [];

    private void ReplaceLoopVariable(TempVariableNode from, TempVariableNode to,
        LoopStatement loop, IStatement preLoop)
    {
        if (!ReferenceEquals(from, to))
        {
            List<TempAssignmentNode> assignments = [];
            new StatementVisitor(loop.Body).Visit(statement =>
            {
                assignments.AddRange(statement.Outputs.Values.OfType<TempAssignmentNode>()
                    .Where(a => ReferenceEquals(a.TempVariable, from) && !assignments.Contains(a)));
            });
            _loopMerges.Add((loop, preLoop, to, from, assignments));
        }
        ReplaceTempVariable(from, to);
    }

    /// <summary>
    /// Undoes a merge from <see cref="UseLoopVariablesInBody"/> where the variable
    /// is dead at the loop head and after the loop: fxc only reused the register,
    /// so e.g. an int load need not live in a float variable.
    /// </summary>
    private void SplitUncarriedLoopValues()
    {
        foreach (var carry in _loopMerges)
        {
            VariableLiveness liveness = VariableLiveness.Analyze(_statements, exact: true);
            if (liveness.MayBeReadAfter(carry.Carried, carry.PreLoop)
                || liveness.MayBeLiveAfter(carry.Carried, carry.Loop))
            {
                continue;
            }
            // Every in-loop assignment must be of the other type, or it would be
            // renamed too.
            List<TempAssignmentNode> inLoop = [];
            new StatementVisitor(carry.Loop.Body).Visit(statement =>
            {
                inLoop.AddRange(statement.Outputs.Values.OfType<TempAssignmentNode>()
                    .Where(a => ReferenceEquals(a.TempVariable, carry.Carried) && !inLoop.Contains(a)));
            });
            if (inLoop.Count == 0 || !inLoop.All(carry.Assignments.Contains))
            {
                continue;
            }
            TempVariableNode own = carry.Own;
            own.Outputs.Clear();
            HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
            var pending = new Stack<HlslTreeNode>();
            // Walk from computed (not carried) values, stopping at other assignments.
            new StatementVisitor(carry.Loop.Body).Visit(statement =>
            {
                foreach (var output in statement.Outputs.ToList())
                {
                    if (ReferenceEquals(output.Value, carry.Carried))
                    {
                        statement.Outputs[output.Key] = own;
                    }
                    else if (statement is not (IfStatement or LoopStatement or SwitchStatement)
                        && !(statement.Inputs.TryGetValue(output.Key, out HlslTreeNode input)
                            && ReferenceEquals(input, output.Value)))
                    {
                        pending.Push(output.Value);
                    }
                }
                foreach (HlslTreeNode held in statement.HeldNodes)
                {
                    pending.Push(held);
                }
                statement.ReplaceHeldNode(carry.Carried, own);
            });
            HashSet<HlslTreeNode> roots = HlslTreeNode.NewNodeSet();
            foreach (HlslTreeNode root in pending)
            {
                roots.Add(root);
            }
            while (pending.Count != 0)
            {
                HlslTreeNode node = pending.Pop();
                if (node == null || node is TempVariableNode || !seen.Add(node))
                {
                    continue;
                }
                if (node is TempAssignmentNode assignment)
                {
                    if (!roots.Contains(node))
                    {
                        continue;
                    }
                    if (ReferenceEquals(assignment.TempVariable, carry.Carried))
                    {
                        assignment.TempVariable = own;
                    }
                }
                for (int i = 0; i < node.Inputs.Count; i++)
                {
                    if (ReferenceEquals(node.Inputs[i], carry.Carried))
                    {
                        node.Inputs[i] = own;
                        carry.Carried.Outputs.Remove(node);
                        own.Outputs.Add(node);
                    }
                    else
                    {
                        pending.Push(node.Inputs[i]);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Rewrites every reference to a variable: graph edges, held nodes, statement
    /// maps and <see cref="TempAssignmentNode.TempVariable"/>, none of which Replace
    /// alone reaches.
    /// </summary>
    private void ReplaceTempVariable(TempVariableNode from, TempVariableNode to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        from.Replace(to);
        ReplaceInStatementNodes(from, to);

        new StatementVisitor(_statements).Visit(statement =>
        {
            foreach (var output in statement.Outputs.Where(o => ReferenceEquals(o.Value, from)).ToList())
            {
                statement.Outputs[output.Key] = to;
            }
            foreach (var input in statement.Inputs.Where(i => ReferenceEquals(i.Value, from)).ToList())
            {
                statement.Inputs[input.Key] = to;
            }
            foreach (var assignment in statement.Outputs.Values
                .Concat(statement.Inputs.Values)
                .OfType<TempAssignmentNode>())
            {
                if (ReferenceEquals(assignment.TempVariable, from))
                {
                    assignment.TempVariable = to;
                    assignment.IsReassignment = true;
                }
            }
        });
    }

    /// <summary>
    /// Registers with a component a statement holds and something else also reads.
    /// By register, since components are named together or not at all.
    /// </summary>
    private HashSet<RegisterComponentKey> SharedWithHoldingStatement(
        IEnumerable<KeyValuePair<RegisterComponentKey, HlslTreeNode>> assignments)
    {
        List<KeyValuePair<RegisterComponentKey, HlslTreeNode>> all = [.. assignments];
        var shared = new HashSet<RegisterComponentKey>();
        foreach (var assignment in all)
        {
            if (!IsSharedWithHoldingStatement(assignment.Value))
            {
                continue;
            }
            shared.Add(assignment.Key);
            // Plus sibling components from the same instruction only.
            foreach (var sibling in all)
            {
                if (!sibling.Key.RegisterKey.Equals(assignment.Key.RegisterKey)
                    || ReferenceEquals(sibling.Value, assignment.Value))
                {
                    continue;
                }
                if (IsSameValue(sibling.Value, assignment.Value))
                {
                    shared.Add(sibling.Key);
                }
            }
        }
        return shared;
    }

    /// <summary>
    /// Written by the same instruction, so components of one value.
    /// </summary>
    private static bool IsSameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return HlslTreeNode.IsSameInstruction(a, b);
    }

    /// <summary>
    /// Held and read elsewhere too, so inlining would compute it twice. A sole
    /// holder inlines the expression.
    /// </summary>
    private bool IsSharedWithHoldingStatement(HlslTreeNode node)
    {
        int holders = 0;
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement.NamedHeldNodes.Contains(node))
            {
                holders++;
            }
        });
        return holders > 1 || (holders == 1 && node.Outputs.Count != 0);
    }

    /// <summary>
    /// Whether an unread value is the one a break/continue carries out of the loop.
    /// No graph node reads it (the loop phi reads the end-of-body value), so it is
    /// decided by register: carried when the loop hands that register on to a
    /// reader. Not by node shape, which misses e.g. a sample taken before a break.
    /// </summary>
    private bool IsCarriedByJump(IStatement jump, HlslTreeNode node)
    {
        if (node.Outputs.Count != 0
            || !_carriedByJump.TryGetValue(jump, out HashSet<RegisterComponentKey> carried))
        {
            return false;
        }
        return jump.Outputs
            .Any(parting => ReferenceEquals(parting.Value, node) && carried.Contains(parting.Key));
    }

    /// <summary>
    /// Per break/continue, the keys its loop hands on to a reader. Computed once
    /// up front: <see cref="InsertTempVariableAssignments"/> clears the loop phi's
    /// readers, and the removal and insertion passes must agree. A break in a
    /// switch keeps the enclosing loop, erring towards observable.
    /// </summary>
    private void FindCarriedByJump()
    {
        FindCarriedByJump(_statements, null);
    }

    private void FindCarriedByJump(IList<IStatement> statements, LoopStatement loop)
    {
        foreach (IStatement statement in statements)
        {
            if (statement is BreakStatement or ContinueStatement && loop != null)
            {
                _carriedByJump[statement] = [.. loop.Outputs
                    .Where(carried => carried.Value.Outputs.Count != 0)
                    .Select(carried => carried.Key)];
            }
            if (statement is IfStatement ifStatement)
            {
                FindCarriedByJump(ifStatement.TrueBody, loop);
                if (ifStatement.FalseBody != null)
                {
                    FindCarriedByJump(ifStatement.FalseBody, loop);
                }
            }
            else if (statement is LoopStatement inner)
            {
                FindCarriedByJump(inner.Body, inner);
            }
            else if (statement is SwitchStatement switchStatement)
            {
                foreach (SwitchCase switchCase in switchStatement.Cases)
                {
                    FindCarriedByJump(switchCase.Body, loop);
                }
            }
        }
    }

    /// <summary>
    /// An output read from a UAV or groupshared memory that a later statement writes
    /// is named where it is read: the return written after the store would read the
    /// stored value instead.
    /// </summary>
    private void NameOutputLoadsBeforeWrites()
    {
        // An output struct is assigned where the value is made, not at the return.
        if (!_hasReturnValue || _hasOutputStruct)
        {
            return;
        }
        List<IStatement> order = [];
        new StatementVisitor(_statements).Visit(order.Add);
        for (int i = 0; i < order.Count; i++)
        {
            if (order[i] is not AssignmentStatement statement)
            {
                continue;
            }
            HashSet<RegisterKey> writtenLater = [.. order.Skip(i + 1).SelectMany(WrittenMemory)];
            if (writtenLater.Count == 0)
            {
                continue;
            }
            foreach (var output in statement.Outputs.Where(o => o.Key.RegisterKey.IsOutput).ToList())
            {
                HlslTreeNode value = output.Value;
                if (value is TempAssignmentNode or TempVariableNode or ConstantNode
                    || IsCarried(statement, output.Key)
                    || !ReadMemory(value).Overlaps(writtenLater))
                {
                    continue;
                }
                var variable = new TempVariableNode
                {
                    IsInteger = _integerOperandAnalysis?.IsIntegerOutputSignature(output.Key) == true,
                };
                statement.Outputs[output.Key] = new TempAssignmentNode(variable, value);
                foreach (IStatement later in order.Skip(i + 1))
                {
                    foreach (var map in new[] { later.Inputs, later.Outputs })
                    {
                        if (map.TryGetValue(output.Key, out HlslTreeNode held) && ReferenceEquals(held, value))
                        {
                            map[output.Key] = variable;
                        }
                    }
                }
            }
        }
    }

    // The UAVs and groupshared memory a statement writes.
    private static IEnumerable<RegisterKey> WrittenMemory(IStatement statement)
    {
        HlslTreeNode destination = statement switch
        {
            StoreStructuredStatement store => store.Destination,
            StoreTypedStatement store => store.Destination,
            AtomicStatement atomic => atomic.Destination,
            _ => null,
        };
        return destination is RegisterInputNode register ? [register.RegisterComponentKey.RegisterKey] : [];
    }

    // The UAVs and groupshared memory an expression loads from, short of variables.
    private static HashSet<RegisterKey> ReadMemory(HlslTreeNode value)
    {
        var read = new HashSet<RegisterKey>();
        var seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (!seen.Add(node) || node is TempVariableNode)
            {
                continue;
            }
            HlslTreeNode loaded = (node as LoadStructuredNode)?.Value ?? (node as ResourceLoadNode)?.Inputs[0];
            if (loaded is RegisterInputNode resource)
            {
                read.Add(resource.RegisterComponentKey.RegisterKey);
            }
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
        return read;
    }

    private bool? OutputTypeHolding(HlslTreeNode node)
    {
        if (_integerOperandAnalysis == null)
        {
            return null;
        }
        var types = new HashSet<bool>();
        new StatementVisitor(_statements).Visit(statement =>
        {
            foreach (var output in statement.Outputs)
            {
                HlslTreeNode value = output.Value;
                while (value is MoveOperation move)
                {
                    value = move.Inputs[0];
                }
                if (output.Key.RegisterKey.IsOutput && ReferenceEquals(value, node)
                    && !IsCarried(statement, output.Key))
                {
                    types.Add(_integerOperandAnalysis.IsIntegerOutputSignature(output.Key));
                }
            }
        });
        return types.Count == 1 ? types.Single() : null;
    }

    private IStatement[] FindHoldingStatements(HlslTreeNode node)
    {
        var holders = new List<IStatement>();
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is IndexableTempStoreStatement store
                && (store.Index == node || store.Values.Contains(node)))
            {
                holders.Add(statement);
            }
            else if (statement is BreakStatement or ContinueStatement
                && IsCarriedByJump(statement, node))
            {
                holders.Add(statement);
            }
        });
        return [.. holders];
    }

    /// <summary>
    /// Whether some statement holds the node by name. Same slot set as
    /// <see cref="ReplaceInStatementNodes"/>, both derived from HeldSlots.
    /// </summary>
    private bool IsRenderedByAnyStatement(HlslTreeNode node)
    {
        bool rendered = false;
        new StatementVisitor(_statements).Visit(statement =>
        {
            rendered |= statement.NamedHeldNodes.Contains(node);
        });
        return rendered;
    }

    /// <summary>
    /// Replace for held references, which are not graph edges.
    /// </summary>
    private void ReplaceInStatementNodes(HlslTreeNode node, HlslTreeNode replacement)
    {
        new StatementVisitor(_statements).Visit(
            statement => statement.ReplaceHeldNode(node, replacement));
    }

    /// <summary>
    /// Whether <paramref name="root"/> reads <paramref name="target"/> along some
    /// path that does not pass through <paramref name="avoid"/>: a read of a value
    /// of its own, not only the one inside it.
    /// </summary>
    private static bool ReadsAvoiding(HlslTreeNode root, HlslTreeNode target, HlslTreeNode avoid)
    {
        var visited = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>();
        stack.Push(root);
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (ReferenceEquals(node, avoid) || !visited.Add(node))
            {
                continue;
            }
            if (ReferenceEquals(node, target))
            {
                return true;
            }
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                stack.Push(input);
            }
        }
        return false;
    }

    /// <summary>
    /// A mask read only as a condition, so it can be a bool. Every reader must only
    /// test for zero (select, if, break): a bool converts to 1, a mask is all ones.
    /// Predicates written out again count as conditions; named held values do not.
    /// </summary>
    private bool HoldsOnlyACondition(HlslTreeNode value, IEnumerable<HlslTreeNode> readers)
    {
        // A mask joined by an if/else (`t = a <= 1` / `t = -1`) and read only as a
        // condition; either branch's value may be the declaring one.
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        if (readers.Any() && readers.All(reader => reader is PhiNode)
            && IsMask(value, seen, out bool compares)
            && readers.All(reader => IsMaskJoin(reader, seen, ref compares)) && compares)
        {
            return true;
        }
        // A comparison, or comparisons combined with && / ||.
        if (value is not (ComparisonNode or LogicalAndOperation or LogicalOrOperation))
        {
            return false;
        }
        // Or compared with another mask, `ieq r0.w, r0.w, r0.y`: as a bool, since
        // an int holds true as 1, not -1.
        if (!readers.All(reader => (reader is MoveConditionalOperation select
                && ReferenceEquals(select.Condition, value))
            || reader is LogicalAndOperation or LogicalOrOperation
            || (reader is ComparisonNode { Comparison: IfComparison.EQ or IfComparison.NE } masks
                && (ReferenceEquals(masks.Left, value) ? masks.Right : masks.Left) is ComparisonNode)))
        {
            return false;
        }
        bool named = false;
        bool tested = false;
        new StatementVisitor(_statements).Visit(statement =>
        {
            named |= statement.NamedHeldNodes.Contains(value);
            tested |= statement.HeldNodes.Contains(value);
        });
        return !named && (tested || readers.Any());
    }

    // A comparison, or a constant all ones or all zeroes.
    private static bool IsMask(HlslTreeNode value, HashSet<HlslTreeNode> seen, out bool compares)
    {
        while (value is MoveOperation move)
        {
            value = move.Inputs[0];
        }
        compares = value is ComparisonNode or LogicalAndOperation or LogicalOrOperation;
        return compares
            || value is ConstantNode { IntegerValue: -1 or 0 }
            || value is ConstantNode { IntegerValue: null, Value: 0 };
    }

    // A join of masks read only by zero tests, select conditions or such joins.
    private static bool IsMaskJoin(HlslTreeNode phi, HashSet<HlslTreeNode> seen, ref bool compares)
    {
        if (phi is not PhiNode)
        {
            return false;
        }
        if (!seen.Add(phi))
        {
            return true;
        }
        foreach (HlslTreeNode input in phi.Inputs)
        {
            if (input is PhiNode)
            {
                if (!IsMaskJoin(input, seen, ref compares))
                {
                    return false;
                }
            }
            else if (IsMask(input, seen, out bool inputCompares))
            {
                compares |= inputCompares;
            }
            else
            {
                return false;
            }
        }
        if (phi.Outputs.Count == 0)
        {
            return false;
        }
        foreach (HlslTreeNode reader in phi.Outputs)
        {
            bool ok = reader switch
            {
                PhiNode => IsMaskJoin(reader, seen, ref compares),
                MoveConditionalOperation select => ReferenceEquals(select.Condition, phi),
                ComparisonNode { Comparison: IfComparison.EQ or IfComparison.NE } test =>
                    ReferenceEquals(test.Left, phi) && test.Right is ConstantNode { Value: 0 },
                _ => false,
            };
            if (!ok)
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsFreeToRead(HlslTreeNode node)
    {
        return node is ConstantNode or RegisterInputNode
            || (node is MoveOperation move && move.Inputs[0] is ConstantNode or RegisterInputNode);
    }

    private void RemoveAnyAssignment(HlslTreeNode node)
    {

        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement.Inputs.Values.Contains(node))
            {
                foreach (var item in statement.Inputs.Where(o => o.Value == node).ToList())
                {
                    statement.Inputs.Remove(item.Key);
                }
            }

            if (statement.Outputs.Values.Contains(node))
            {
                foreach (var item in statement.Outputs.Where(o => o.Value == node).ToList())
                {
                    statement.Outputs.Remove(item.Key);
                }
            }
        });
    }

    /// <summary>
    /// Whether an input of the phi, or of a nested join, is the variable or its assignment.
    /// </summary>
    private static bool Merges(PhiNode phi, TempVariableNode variable)
    {
        return Merges(phi, variable, HlslTreeNode.NewNodeSet());
    }

    private static bool Merges(PhiNode phi, TempVariableNode variable, HashSet<HlslTreeNode> seen)
    {
        if (!seen.Add(phi))
        {
            return false;
        }
        foreach (HlslTreeNode input in phi.Inputs)
        {
            if (ReferenceEquals(input, variable)
                || (input is TempAssignmentNode assignment && ReferenceEquals(assignment.TempVariable, variable))
                || (input is PhiNode inner && Merges(inner, variable, seen)))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether a value reads the variable through arithmetic only: not through
    /// another variable, and not through a load (a sample's coordinate does not make
    /// the result a continuation of it).
    /// </summary>
    private static bool Reads(HlslTreeNode value, TempVariableNode variable)
    {
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (!seen.Add(node))
            {
                continue;
            }
            if (ReferenceEquals(node, variable))
            {
                return true;
            }
            if (node is TempVariableNode || node is PhiNode
                || (node is not Operation && node is not GroupNode && node is not ComparisonNode))
            {
                continue;
            }
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
        return false;
    }

    private void ReplaceAnyAssignment(RegisterComponentKey componentKey, HlslTreeNode node, HlslTreeNode replacement)
    {
        new StatementVisitor(_statements).Visit(s =>
        {
            if (s.Inputs.TryGetValue(componentKey, out var input) && input == node)
            {
                s.Inputs[componentKey] = replacement;
            }
            if (s.Outputs.TryGetValue(componentKey, out var output) && output == node)
            {
                s.Outputs[componentKey] = replacement;
            }
        });
    }
}
