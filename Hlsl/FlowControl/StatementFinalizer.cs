using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class StatementFinalizer
{
    private IList<IStatement> _statements;
    private bool _hasReturnValue;
    // Whether the outputs leave through a struct variable rather than as the one
    // returned expression. It decides where a return goes when the shader ends on an
    // if and an else: with a struct they can share one after the branches, and
    // without one each branch has to carry its own value out.
    private readonly bool _hasOutputStruct;
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;
    private readonly ISet<HlslTreeNode> _doubleValues;
    // The registers the caller of this function reads when it returns - a linkage
    // body's result, which nothing inside the body reads, being read out of it.
    // The dead-value pass below would otherwise take it out as computing nothing.
    private readonly HashSet<RegisterKey> _liveOut;

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

    private void FinalizeStatements()
    {
        RemoveUnusedAssignmentInputOutput();
        RemoveUnusedAssignments(_statements);
        InsertTempVariableAssignments(_statements);
        LowerResolvedPhis();
        LoopRecovery.Recover(_statements);
        SetReturnStatement(_statements);
        SplitUncarriedLoopValues();
        CoalesceCopies();
        LocalizeDeadJoins();
    }

    /// <summary>
    /// A variable an if hands on that nothing reads after the if, assigned in one
    /// branch only, declared in that branch instead. The finalizer joins whatever
    /// the branches leave in a register, which declares the variable above the if
    /// for both branches to assign - and a decal's position, read by nothing but
    /// the test in the same branch, came out as `float2 t0;` at the top of the
    /// function and `t0 = mul(...)` deep inside, a reassignment, and nothing can be
    /// done with a variable whose declaration is somewhere else. Asked of
    /// VariableLiveness, after everything else, so the answer is the one the
    /// writer gets.
    ///
    /// The if and everything after it stop handing the variable on: they hand on
    /// what the register held before the if, or nothing where it held nothing.
    /// Neither is read, which is what dead means. The outermost if goes first, so
    /// that each level puts back what its own register held on the way in.
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
                // The first assignment in the branch is the declaration now.
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

    // The branch of the if every assignment to the variable is in, or null where
    // they are in both, or anywhere outside it.
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
    /// Copies of one variable into another that never needs to differ from it, made
    /// one variable. An inner loop works on a copy of the outer loop's accumulator,
    /// because fxc gave it a register of its own, and copies it back when it is
    /// done: `float4 t1 = t0; for (...) { t1 = t1 + colour; } t0 = t1;`. Where the
    /// two are never both wanted holding different values - the outer one is not
    /// read again until the copy back, and the copy is not read after the outer one
    /// is assigned anything else - they are one variable, and the loop works on t0.
    ///
    /// That is the interference question, asked of VariableLiveness: neither
    /// variable may be live where the other is assigned, except at the copies
    /// between them. A copy is all of a statement's lanes or none of them, so that
    /// a vector is never left half one variable and half the other.
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
                    || !copies.All(copy => CanCoalesce(copy.Copy, copy.Source, statement, statements, liveness)))
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
            || IsLoopClauseVariable(copy) || IsLoopClauseVariable(source))
        {
            return false;
        }
        foreach (IStatement statement in statements)
        {
            HashSet<TempVariableNode> writes = VariableLiveness.Writes(statement);
            // Where the copy is assigned anything but the source, the source must not
            // be wanted afterwards, or the one variable would hold the copy's value
            // where the source's was still to be read.
            if (writes.Contains(copy) && !ReferenceEquals(statement, copyStatement)
                && liveness.MayBeLiveAfter(source, statement))
            {
                return false;
            }
            // And the other way about, except where the source is assigned the copy
            // back - the same value either way.
            if (writes.Contains(source) && !CopiesBack(statement, source, copy)
                && liveness.MayBeLiveAfter(copy, statement))
            {
                return false;
            }
        }
        return true;
    }

    // Whether the statement assigns the variable nothing but the other one.
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

    // What coalescing leaves behind: the copy and the copy back are the variable
    // assigned itself. The register holds the variable as it is, which the writer
    // writes nothing for.
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
    /// A join phi whose every leaf is one variable is that variable. Most are
    /// replaced as the branches are lowered; one that merges another join - an if
    /// inside the then branch of an if/else hands its own join up - is not, since
    /// the inner join is a phi and not the variable it stands for until the
    /// branches are unified, and the statements after the outer if went on holding
    /// it: it reached the writer as a phi of phis.
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
                // A mova nothing reads - the read through the address register took
                // the element off the product the mova was given, and reads that
                // instead - still read the product, and kept `float t0 = 4 * idx`
                // alive as a name for it.
                if (output.Key.RegisterKey is D3D9RegisterKey { Type: RegisterType.Addr }
                    && output.Value is MoveOperation && output.Value.Outputs.Count == 0)
                {
                    output.Value.Remove();
                }
            }

            // A phi nobody reads keeps its inputs alive for nothing. It can be the
            // output of any block, not just an assignment - an if whose branches all
            // return still merges what they wrote.
            // A phi a store still carries is not dead, whatever the node graph says:
            // the reader is the store rather than a node. Removed on the graph's word
            // alone it was disconnected from its inputs as well, so the branch value
            // feeding it lost its only reader, went unnamed, and the phi - still sitting
            // in the store - reached the compiler unlowered.
            outputsToRemove = statement.Outputs
                .Where(o => o.Value is PhiNode && o.Value.Outputs.Count == 0)
                .Where(o => !IsRenderedByAnyStatement(o.Value))
                .ToList();
            foreach (var output in outputsToRemove)
            {
                statement.Outputs.Remove(output.Key);
                output.Value.Remove();
            }
        });
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
            // Only what this statement computes: a value carried through unchanged
            // from its inputs belongs to the statement that computed it. Judging it
            // here, in a loop body whose own outputs read it, found every consumer
            // inside the body and dropped the assignment from the statement before
            // the loop as well, which then inlined a loop invariant into every use.
            var assignmentOutputs = assignment.Outputs
                .Where(o => o.Key.RegisterKey.IsTempRegister)
                .Where(o => !(assignment.Inputs.TryGetValue(o.Key, out var input) && ReferenceEquals(input, o.Value)))
                // What the caller reads on the way out is read, whoever the
                // reader is: a body's result has no reader inside the body, and
                // nothing here can see the fcall that goes looking for it.
                .Where(o => !_liveOut.Contains(o.Key.RegisterKey))
                .ToDictionary();
            // Which registers this statement assigns have a component shared with a
            // statement that holds it. By register rather than by component, since a
            // load writes four and only the one a condition reads looks shared - and
            // by register rather than by statement, since one statement carries
            // several: the comparison beside the load is not the load.
            HashSet<RegisterComponentKey> sharedRegisters = SharedWithHoldingStatement(assignmentOutputs);
            foreach (var assignmentOutput in assignmentOutputs)
            {
                var assignmentNode = assignmentOutput.Value;

                // A constant or a plain register read is never worth a variable,
                // wherever it is read - a literal in a loop body is a literal, and an
                // input is an input. Unless a phi reads it: then it is the value a
                // loop counter or accumulator starts from, and the variable is the
                // point.
                if (IsFreeToRead(assignmentNode) && assignmentNode.Outputs.All(v => v is not PhiNode))
                {
                    RemoveAnyAssignment(assignmentNode);
                    continue;
                }

                // Check if assignment output goes only into itself. A phi consumer
                // means the value leaves the statement - to a branch join, or along a
                // loop backedge into the next iteration - so it is still live.
                if (assignmentNode.Outputs.All(v => v is not PhiNode && v.IsInputOf(assignment.Outputs.Values)))
                {
                    // A store into a local array holds its value without being a
                    // node that reads it. Written into the very next statement the
                    // value can be inlined there; held any further away it has to be
                    // named here, since a store in between may change what the value
                    // reads - a swap loads both elements before it writes either.
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
                        // Only when the clip is all that reads it. A sample whose
                        // alpha feeds the clip and the colour after it was dropped
                        // here and re-read at every later use.
                        if (assignmentNode.IsInputOf(clip.Values)
                            && assignmentNode.Outputs.All(o => o.IsInputOf(clip.Values)))
                        {
                            assignment.Outputs.Remove(assignmentOutput.Key);
                            clip.Inputs.Remove(assignmentOutput.Key);
                        }
                    }
                    else if (nextStatement is IfStatement ifStatement)
                    {
                        // The condition inlines the value, so an assignment feeding
                        // nothing but the comparison is dead. Keeping it wrote a
                        // `t0 = a < b;` that nothing had declared, of a type that a
                        // temp cannot hold anyway.
                        if (assignmentNode.IsInputOf(ifStatement.Comparison)
                            && assignmentNode.Outputs.All(o => o.IsInputOf(ifStatement.Comparison))
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
            // Which of these values feeds which, taken now because lowering is about
            // to replace each of them with a variable. After that a read of the value
            // this statement computes and a read of the one the register held before
            // are the same name, and nothing tells them apart.
            var feeds = new Dictionary<RegisterComponentKey, List<RegisterComponentKey>>();
            foreach (var reader in newAssignments)
            {
                feeds[reader.Key] = [.. newAssignments
                    .Where(fed => !ReferenceEquals(fed.Value, reader.Value)
                        && fed.Value.IsInputOf(reader.Value))
                    .Select(fed => fed.Key)];
            }
            // The same for the output registers: one that reads a value this statement
            // assigns to a temp has to be written after that assignment, and one that
            // reads what the register held before has to be written before it. An
            // output whose value *is* the temp's value is neither - it recomputes it
            // from the old variable, and the sort's overwrite rule handles that.
            var outputFeeds = new Dictionary<RegisterComponentKey, List<RegisterComponentKey>>();
            foreach (var output in statement.Outputs.Where(o => !o.Key.RegisterKey.IsTempRegister))
            {
                outputFeeds[output.Key] = [.. newAssignments
                    .Where(fed => !ReferenceEquals(fed.Value, output.Value)
                        && fed.Value.IsInputOf(output.Value))
                    .Select(fed => fed.Key)];
            }
            // By register, for the reason the removal above asks it that way.
            HashSet<RegisterComponentKey> sharedRegisters = SharedWithHoldingStatement(newAssignments);
            var assignmentByKey = new Dictionary<RegisterComponentKey, TempAssignmentNode>();
            foreach (var newAssignment in newAssignments)
            {
                HlslTreeNode tempValue = newAssignment.Value;

                // Insert temp variable if value has output outside of current statement
                // or if an iteration variable is changed. A store statement holding the
                // value is a use outside the statement too.
                bool doesOutputExitStatement = tempValue.Outputs.Any(v => !v.IsInputOf(statement.Outputs.Values))
                    || FindHoldingStatements(tempValue).Length != 0
                    // And the same question the removal above asks: a value a store
                    // or an append holds alongside another reader has left the
                    // statement, whatever the node graph says about it.
                    || sharedRegisters.Contains(newAssignment.Key);
                statement.Inputs.TryGetValue(newAssignment.Key, out var inputAssignment);
                var tempInputAssignment = inputAssignment as TempAssignmentNode;
                TempVariableNode tempInputVariable = GetExistingVariable(inputAssignment);
                // A register whose value is a variable already - the old value an
                // interlocked operation hands back through its out parameter - is that
                // variable, read where it is. Given one of its own it was copied the
                // moment the call returned: `int t1 = t0;`, and t1 read from then on.
                // Not where the register was assigned before, or feeds a phi: there
                // the copy is the assignment that carries the value on.
                if (tempValue is TempVariableNode && tempInputAssignment == null && tempInputVariable == null
                    && !tempValue.Outputs.Any(usage => usage is PhiNode))
                {
                    continue;
                }
                if (doesOutputExitStatement || tempInputAssignment != null)
                {
                    List<HlslTreeNode> tempUsages = tempValue.Outputs.ToList();
                    // Before the readers move to the variable: they are what types it.
                    bool? isIntegerValue = ValueTypes.IsIntegerValue(tempValue);
                    bool? isUnsignedValue = ValueTypes.IsUnsignedValue(tempValue);
                    bool isInteger = isIntegerValue
                        ?? (_integerOperandAnalysis?.IsIntegerRegister(newAssignment.Key) == true);
                    // Also before the clear, and not only because it reads better
                    // there: whether the bits are a float's is a question about the
                    // readers as much as the maker, and there are none afterwards.
                    bool isBits = ValueTypes.IsBitsVariable(tempValue, isInteger);
                    tempValue.Outputs.Clear();
                    // A register fxc reuses for a value of the other type is not
                    // one variable: a flag register that goes on to hold a dot
                    // product is an int variable assigned asint of a float, and
                    // every read of it after that reads the bits. Where the value
                    // says what it is and the variable already there says
                    // otherwise, the value gets a variable of its own - unless the
                    // value feeds a phi over the variable, which is what makes it
                    // that variable in the first place.
                    TempVariableNode existing = tempInputAssignment?.TempVariable ?? tempInputVariable;
                    // And a value that has nothing to do with the one before it gets a
                    // variable of its own too. The register had the other value in
                    // it, which is fxc's allocation and not the shader's: a decal's
                    // position and the colour it blends were one float3 because they
                    // took turns in r1, and the position's third row, overwritten
                    // within the statement, could then be no part of it. Where the
                    // new value reads the old one - `t = t + x` - it is the same
                    // variable going on, and where it feeds a phi over the variable
                    // it has to be - not a phi over values of its own, which is two
                    // branches agreeing with each other and nothing to do with what
                    // the register held before the if.
                    if (existing != null
                        && ((isIntegerValue != null && existing.IsInteger != isIntegerValue
                                && !tempUsages.Any(u => u is PhiNode))
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
                            // Not for bits: those are a float's, and calling them uint
                            // says something about them that is not so.
                            IsUnsigned = isInteger && !isBits && isUnsignedValue == true,
                            // A double is one because the instruction that wrote it
                            // said so: the arithmetic over it is the same addition
                            // and multiply whatever its operands are made of, so
                            // neither the value nor its readers can be asked, and
                            // the register cannot either - fxc reuses one for a
                            // double here and a float there.
                            IsDouble = !isInteger && _doubleValues.Contains(tempValue),
                            IsBool = HoldsOnlyACondition(tempValue, tempUsages),
                        };
                    var tempAssignment = new TempAssignmentNode(tempVariable, tempValue);
                    // The value entering a loop header declares the variable; everything
                    // else that feeds a phi - a branch join, or the loop backedge - is
                    // assigning to a variable that already exists.
                    bool declaresLoopVariable = tempUsages.Count != 0
                        && tempUsages.All(u => u is PhiNode phi
                            && phi.IsLoopHeader
                            && ReferenceEquals(phi.PreLoopValue, tempValue));
                    // A value with no node reading it - one held by a store statement
                    // alone - is not "all phis"; it is a fresh declaration.
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
                            // Nothing reads the phi through those edges any more. Leaving
                            // them means a second pass over the same phi - the statement is
                            // reached both at the top level and through the if it belongs
                            // to - rewires consumers that have already moved and registers
                            // them against a second variable.
                            tempUsage.Outputs.Clear();
                            ReplaceInStatementNodes(tempUsage, tempVariable);
                        }
                        // Keep the back-reference, so that rewiring the variable later -
                        // unifying the branches of an if onto one variable, say - can find
                        // the uses, the merging phi among them.
                        tempVariable.Outputs.Add(tempUsage);
                        int index = tempUsage.Inputs.IndexOf(tempValue);
                        tempUsage.Inputs[index] = tempVariable;
                    }
                    // A statement holding the value reads the variable from now on.
                    ReplaceInStatementNodes(tempValue, tempVariable);
                    ReplaceAnyAssignment(newAssignment.Key, tempValue, tempAssignment);
                    assignmentByKey[newAssignment.Key] = tempAssignment;
                }
            }

            // Once every assignment exists, the record can name them. The
            // assignment rather than its variable: branch and loop unification
            // re-points variables afterwards, and the assignment stays itself.
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

    // An if hands out the single variable its branches assigned, either directly or
    // through the phi that merges them. A register arriving that way already has a
    // variable, so assigning it again must not declare a second one.
    private static TempVariableNode GetExistingVariable(HlslTreeNode inputAssignment)
    {
        if (inputAssignment is TempVariableNode variable)
        {
            return variable;
        }
        // A loop header phi is left alone: its variable is declared before the loop,
        // which the backedge handling below already accounts for. A join phi may
        // merge another join phi - an if inside the then branch of an if/else
        // hands its own join up - so the question is asked of each input in turn:
        // a phi of phis is the one variable when every leaf is.
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
    /// Both branches of an if assign a register through one variable, declared above
    /// the if - a declaration inside a branch would go out of scope at its closing
    /// brace. The first branch to assign a register owns the variable, the others are
    /// rewired onto it, and every branch assignment is therefore a reassignment.
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
                // A branch either assigns the register itself, or hands out the
                // variable a nested if already merged its own branches into.
                var assignment = output.Value as TempAssignmentNode;
                TempVariableNode branchVariable =
                    assignment?.TempVariable ?? output.Value as TempVariableNode;
                if (branchVariable == null)
                {
                    continue;
                }
                // A register the branch only carries through keeps the node it
                // entered with. It is assigned elsewhere, so it is not the
                // branch's to declare or reassign.
                if (ifStatement.Inputs.TryGetValue(output.Key, out HlslTreeNode entryValue)
                    && ReferenceEquals(entryValue, output.Value))
                {
                    continue;
                }
                if (variableByRegister.TryGetValue(output.Key, out TempVariableNode variable))
                {
                    // The branches can already share the variable: two ifs in a row
                    // assigning the same register hand the second one a phi over it,
                    // and both of its branches then reuse that one variable.
                    if (!ReferenceEquals(branchVariable, variable))
                    {
                        branchVariable.Replace(variable);
                        // And wherever a statement holds the variable rather than
                        // reading it through the graph: a branch that stores what it
                        // just computed holds that value as the store's own, and
                        // Replace reaches nothing there. Left behind, the store asked
                        // for the variable this branch has just given up - a name
                        // nothing declares any more.
                        ReplaceInStatementNodes(branchVariable, variable);
                        if (assignment != null)
                        {
                            assignment.TempVariable = variable;
                        }
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
    /// Every case that assigns a register must assign the same variable, the way the
    /// two branches of an if/else do. The first case to assign it owns the variable;
    /// the rest reassign it, and the switch carries it out.
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

        // A case whose body ends in a switch or an if of its own carries a phi out
        // of it: that statement joined its own branches onto a variable of its own,
        // and the break after it still holds the join. The variable is what the case
        // assigns, and taking the phi for nothing left the register unwritten on
        // that path, so the case read as zero - the nested switch bug.
        //
        // After the plain assignments, not among them, because the variable the
        // enclosing scope goes on to read is one of theirs. Letting a variable
        // created inside the nested statement own the register instead left the
        // switch carrying one variable and the return reading another.
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

        foreach (var entry in variableByRegister)
        {
            switchStatement.Outputs[entry.Key] = entry.Value;
        }
    }

    /// <summary>
    /// The variable the case last put the register in, looking back past the break
    /// to the statement that did the assigning.
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
    /// Points every assignment and every read of one variable inside these statements
    /// at another, for a case that carries its result out of a nested statement while
    /// an earlier case already owns the variable the switch will carry.
    /// </summary>
    private void Reassign(
        IList<IStatement> body, TempVariableNode from, TempVariableNode to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        // The reads as well as the assignments. A loop inside the case reads the
        // variable it carries over its own backedge, and that read is a graph edge
        // rather than an assignment: rewriting only the assignments left it naming
        // the variable this pass has just stopped anything from declaring, and the
        // writer numbered that name for itself on the way past.
        from.Replace(to);
        // And the reads that are not graph edges at all - the value or the address
        // of a store the case makes. A store of the variable the case carries went
        // the same way for the same reason: `output[t1] = t1` of a name nothing
        // declares, where the case had just been put onto a variable of its own.
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
            // One return after the branches where the outputs leave through a struct:
            // the branches filled it, and returning it once is what the bytecode does
            // - it falls through the endif to a single ret. Pushed into each branch
            // instead it cost an instruction apiece, since fxc then writes a ret in
            // each of them.
            if (ifStatement.FalseBody != null && !_hasOutputStruct)
            {
                SetReturnStatement(ifStatement.TrueBody);
                SetReturnStatement(ifStatement.FalseBody);
            }
            else
            {
                // Without an else branch the fall-through path needs its own return.
                statements.Add(new ReturnStatement(ifStatement.Outputs));
            }
            return;
        }
        if (lastStatement is LoopStatement || lastStatement is ClipStatement
            || lastStatement is DiscardStatement
            || lastStatement is BreakStatement || lastStatement is ContinueStatement
            || lastStatement is SwitchStatement
            // A statement that does its work by side effect ends a shader that
            // returns nothing, and those returned above. One that has a value to
            // return still has to return it: a pixel shader storing to a buffer and
            // then answering a colour ends on the store, and left as the end of it
            // the colour went unwritten - output with no return in it at all.
            || lastStatement is AppendStatement
            || lastStatement is StoreStructuredStatement
            || lastStatement is StoreTypedStatement
            || lastStatement is BufferAppendStatement
            || lastStatement is AtomicStatement
            || lastStatement is IndexableTempStoreStatement
            || lastStatement is RestartStripStatement
            || lastStatement is SyncStatement)
        {
            // Return after the statement, not in place of it.
            statements.Add(new ReturnStatement(lastStatement.Outputs));
            return;
        }
        throw new NotImplementedException(lastStatement.GetType().Name);
    }

    /// <summary>
    /// A register carried through a loop must reuse the variable declared before it,
    /// wherever in the body it is assigned - not only in the body's last statement.
    /// An assignment inside a branch, or before a <c>continue</c>, is just as much a
    /// write to the loop-carried variable. A register written only inside the body
    /// has no counterpart before the loop and keeps its own variable.
    /// </summary>
    private void UseLoopVariablesInBody(LoopStatement loopStatement, AssignmentStatement preLoopStatement)
    {
        new StatementVisitor(loopStatement.Body).Visit(bodyStatement =>
        {
            foreach (var bodyOutput in bodyStatement.Outputs.ToList())
            {
                // Assigned before the loop, or a variable carried on into it as it is.
                if (!preLoopStatement.Outputs.TryGetValue(bodyOutput.Key, out var preLoopValue)
                    || ((preLoopValue as TempAssignmentNode)?.TempVariable ?? preLoopValue as TempVariableNode)
                        is not TempVariableNode loopVariable)
                {
                    continue;
                }

                if (bodyOutput.Value is TempAssignmentNode bodyAssignment)
                {
                    // Not just the property: whatever the body already rewired to read
                    // the old variable goes on reading it, and comes out as a name
                    // nothing ever assigns.
                    ReplaceLoopVariable(bodyAssignment.TempVariable, loopVariable, loopStatement, preLoopStatement);
                }
                else if (bodyOutput.Value is TempVariableNode joinVariable)
                {
                    // The value came from a branch join rather than a plain assignment.
                    // The join allocated its own variable; it is the loop-carried one.
                    ReplaceLoopVariable(joinVariable, loopVariable, loopStatement, preLoopStatement);
                }
                else if (bodyOutput.Value is PhiNode joinPhi && !joinPhi.IsLoopHeader)
                {
                    // Same case, but the statement still holds the unlowered join phi.
                    // Lowering the branches rewrote its operands to the variable they
                    // share, so the join variable is reachable through them.
                    foreach (var branchVariable in joinPhi.Inputs.OfType<TempVariableNode>().ToList())
                    {
                        ReplaceTempVariable(branchVariable, loopVariable);
                    }
                }
            }
        });
    }

    // The body's variables given the loop's where the two disagree about type,
    // for SplitUncarriedLoopValues to look at once liveness can be asked.
    private readonly List<(LoopStatement Loop, IStatement PreLoop, TempVariableNode Carried,
        TempVariableNode Own, List<TempAssignmentNode> Assignments)> _typeChangingCarries = [];

    private void ReplaceLoopVariable(TempVariableNode from, TempVariableNode to,
        LoopStatement loop, IStatement preLoop)
    {
        if (!ReferenceEquals(from, to)
            && (from.IsInteger != to.IsInteger || from.IsDouble != to.IsDouble))
        {
            List<TempAssignmentNode> assignments = [];
            new StatementVisitor(loop.Body).Visit(statement =>
            {
                assignments.AddRange(statement.Outputs.Values.OfType<TempAssignmentNode>()
                    .Where(a => ReferenceEquals(a.TempVariable, from) && !assignments.Contains(a)));
            });
            _typeChangingCarries.Add((loop, preLoop, to, from, assignments));
        }
        ReplaceTempVariable(from, to);
    }

    /// <summary>
    /// A register a loop's body assigns a value of another type to, given back the
    /// variable of its own type where the loop carries nothing in it.
    ///
    /// UseLoopVariablesInBody gives every register the body assigns the variable it
    /// had before the loop, on the reading that the loop carries it round. Which is
    /// so where something reads it at the head of the loop or after it, and not
    /// where fxc has only reused the register: tile_luminance loads an int from
    /// groupshared memory into the register a float sample had held before the
    /// loop, the load was declared in the float's variable, and the sum it went
    /// into was added as floats - exact for the counts it holds, and not for an int
    /// past 2^24. Where liveness says the variable is wanted neither at the head of
    /// the loop nor after it, every value of it inside the loop is one the body
    /// assigned, and those go back to the variable they were lowered with.
    /// </summary>
    private void SplitUncarriedLoopValues()
    {
        foreach (var carry in _typeChangingCarries)
        {
            VariableLiveness liveness = VariableLiveness.Analyze(_statements, exact: true);
            if (liveness.MayBeReadAfter(carry.Carried, carry.PreLoop)
                || liveness.MayBeLiveAfter(carry.Carried, carry.Loop))
            {
                continue;
            }
            // Every assignment of the variable in the loop has to be one that came
            // with the other type; a value of the loop variable's own type assigned
            // there as well would be renamed with them.
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
            // From what each statement in the loop computes - not every value its
            // map holds, which includes registers carried through from before the
            // loop - and not on past another assignment, which is a value computed
            // elsewhere and read by name.
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
    /// Rewrites every reference to one temp variable so it uses another, across the
    /// graph and across every statement. <see cref="TempAssignmentNode.TempVariable"/>
    /// is a property rather than a graph input, so it needs its own pass, and so do
    /// the nodes a statement holds beside its maps - the value and address of a
    /// store, the values of an append or a clip. Those are not edges in the graph,
    /// so Replace never reaches them: a store left holding the variable this one
    /// supersedes asked for a name nothing ever declares, which is what a loop whose
    /// body stores the counter after a `continue` came out as.
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
                    // The variable already exists; this is no longer a declaration.
                    assignment.IsReassignment = true;
                }
            }
        });
    }

    // A statement can hold nodes outside its input and output maps - the address and
    // values of a store, the values of a clip. Those are not consumers in the graph,
    // so rewiring by output list never reaches them, and a phi left behind there
    // reaches compilation unlowered.
    // The local array stores that hold a node directly - as their value or their
    // index - rather than reading it through the graph. A structured store or a
    // clip holds its values the same way, but they inline whatever they hold
    // wherever it was computed, as they always have; only the local array, whose
    // stores can change what an earlier load meant, is made to keep the distance.
    /// <summary>
    /// The registers among these assignments with a component that a statement
    /// holds and something else reads as well. Asked of the register because the
    /// components of one are named together or not at all.
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
            // And the components that belong with it. A load writes four and only
            // the one a condition reads looks shared, so naming that one alone
            // leaves the other three as an expression with nowhere to go - and a
            // statement carries several instructions, so its other registers, and
            // even its other writes to this one, are values of their own.
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
    /// Whether two components were written by the same instruction, which is what
    /// makes them components of one value rather than two that share a register.
    /// The same kind of node over the same inputs but for the component each reads.
    /// </summary>
    /// <summary>
    /// Whether two components were written by the same instruction, which is what
    /// makes them components of one value rather than two that share a register.
    /// The node carries which instruction made it, so this is that question and
    /// nothing else. It used to be asked of the node type and the operands, which
    /// two loads of one register answer the same way - and which needed the
    /// resource swizzle and the offset immediate read back out to tell apart.
    /// </summary>
    private static bool IsSameValue(HlslTreeNode a, HlslTreeNode b)
    {
        return HlslTreeNode.IsSameInstruction(a, b);
    }

    /// <summary>
    /// Whether a value a statement holds is read anywhere else as well. One holder
    /// and nothing else needs no variable - the store writes the expression where
    /// it stands, which is how every buffer write in the corpus reads. Two of them,
    /// or one beside a reader in the graph, is a value the bytecode computed once,
    /// and inlining it computes it again at each.
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
    /// Whether a value read by nothing is one a break or a continue carries out of
    /// the loop rather than one that is simply dead. A copy is the usual shape, and
    /// so is the step of a counter fxc worked out in the branch - `iadd r0.z, r0.w,
    /// l(1)` before a continue, and again at the end of the body. Both are read by
    /// nothing where they stand, because the phi that closes the loop reads the one
    /// at the end; dropping the one before the jump stops the loop advancing on
    /// that path. Anything else a jump happens to be live across - the comparison a
    /// clip beside it tests - is dead where it looks dead, and holding it writes an
    /// assignment that nothing declared. An add of a constant and nothing looser:
    /// the row a matrix multiply is read through is `t0 * 4`, dead once the
    /// multiply is recognised, and a multiply by a constant held that too.
    /// </summary>
    private static bool IsCarriedByJump(HlslTreeNode node)
    {
        if (node.Outputs.Count != 0)
        {
            return false;
        }
        return node is MoveOperation
            || (node is AddOperation && node.Inputs.Any(input => input is ConstantNode));
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
            // A break or a continue carries the values of the moment out of the
            // loop, and holds them the same way a store does: nothing in the value
            // graph reads them, because the reader is the loop's exit rather than a
            // node. fxc copies a loop carried value into its register before a break
            // and again at the end of the body; the second is read by the phi that
            // closes the loop and the first by nothing, so the first was removed as
            // dead and a ray march lost its last step.
            else if (statement is BreakStatement or ContinueStatement
                && IsCarriedByJump(node)
                && statement.Outputs.Values.Contains(node))
            {
                holders.Add(statement);
            }
        });
        return [.. holders];
    }

    /// <summary>
    /// Whether the statement renders the node in a slot of its own - the value a store
    /// writes, the address it writes through, the coordinate of a texel, what an
    /// interlocked operation compares against - rather than merely recording it as
    /// some register's value in passing.
    ///
    /// These are exactly the slots ReplaceInStatementNodes rewires, and the two have
    /// to agree on the same set: a value a store carries is read by that store, however
    /// little the node graph says about it, and a register slot on an if is bookkeeping
    /// that the writer never renders. Listing only indexable temp stores here, while
    /// rewiring six kinds of statement there, is what let a value a structured store
    /// carried be judged dead. Both now ask the statement - this one through
    /// NamedHeldNodes and that one through ReplaceHeldNode - so the two sets are the
    /// same set by construction and cannot drift apart again.
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
    /// Rewrites the values the statements hold outside their maps, which the
    /// graph's own Replace does not reach: a held reference is not an input edge,
    /// so nothing in the node points back at the statement holding it.
    /// </summary>
    private void ReplaceInStatementNodes(HlslTreeNode node, HlslTreeNode replacement)
    {
        new StatementVisitor(_statements).Visit(
            statement => statement.ReplaceHeldNode(node, replacement));
    }

    /// <summary>
    /// Whether this value is a comparison that nothing does arithmetic with - a mask
    /// read only as a condition, which is what a bool variable is for.
    ///
    /// Every reader has to be a condition. A mask is all ones where a bool promoted
    /// to a number is one, so anything that adds it, masks with it or stores it
    /// reads a different value if the declaration changes; a select, an if and a
    /// break only ask whether it is zero.
    ///
    /// A predicate a statement holds counts as a condition and is not in the
    /// readers: the branch tests it without reading it through the graph. That is
    /// what NamedHeldNodes tells apart - held and written out again, as against held
    /// and read by name - so a value held only that way is a condition and a value
    /// some statement names is not.
    /// </summary>
    private bool HoldsOnlyACondition(HlslTreeNode value, IEnumerable<HlslTreeNode> readers)
    {
        // A flag an if/else joins - `t = a <= 1` in one branch, `t = -1` in the
        // other, as decal_blend's is - holds a mask whichever branch ran, and a
        // join read only as a condition is one too. Declared int, the comparison
        // is normalised to 0 or 1 on the way in, which is the `and` the shader
        // did not have. Whichever of the values is lowered first declares the
        // variable, so a mask constant into such a join counts the same.
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        if (readers.Any() && readers.All(reader => reader is PhiNode)
            && IsMask(value, seen, out bool compares)
            && readers.All(reader => IsMaskJoin(reader, seen, ref compares)) && compares)
        {
            return true;
        }
        // A comparison, or conditions combined - `any(m) && !all(m)` is as much a
        // mask as either half of it.
        if (value is not (ComparisonNode or LogicalAndOperation or LogicalOrOperation))
        {
            return false;
        }
        if (!readers.All(reader => reader is MoveConditionalOperation select
            && ReferenceEquals(select.Condition, value)))
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

    // A join of masks that nothing reads but a test against zero, a select's
    // condition, or another such join.
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
    /// Whether a phi merges the variable with what else reaches it: one of its
    /// inputs is the variable, or the assignment to it, or a branch join that does -
    /// a loop's pre-loop value, or the register an if leaves alone on one side.
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
    /// Whether a value reads a variable, directly or through the arithmetic that
    /// makes it - not through another variable, which is a value of its own, and not
    /// through a load: a blended colour that samples at the decal's position reads
    /// the position for the coordinate, which makes the colour no more the position
    /// going on than any other value computed from it.
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
