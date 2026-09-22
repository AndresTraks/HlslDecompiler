using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

public class StatementFinalizer
{
    private IList<IStatement> _statements;
    private bool _hasReturnValue;
    private readonly IntegerOperandAnalysis _integerOperandAnalysis;

    private StatementFinalizer(IList<IStatement> statements, bool hasReturnValue,
        IntegerOperandAnalysis integerOperandAnalysis)
    {
        _statements = statements;
        _hasReturnValue = hasReturnValue;
        _integerOperandAnalysis = integerOperandAnalysis;
    }

    public static void Finalize(IList<IStatement> statements, bool hasReturnValue,
        IntegerOperandAnalysis integerOperandAnalysis = null)
    {
        var finalizer = new StatementFinalizer(statements, hasReturnValue, integerOperandAnalysis);
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
            outputsToRemove = statement.Outputs
                .Where(o => o.Value is PhiNode && o.Value.Outputs.Count == 0)
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
                if (doesOutputExitStatement || tempInputAssignment != null)
                {
                    List<HlslTreeNode> tempUsages = tempValue.Outputs.ToList();
                    // Before the readers move to the variable: they are what types it.
                    bool? isIntegerValue = IsIntegerValue(tempValue);
                    bool? isUnsignedValue = IsUnsignedValue(tempValue);
                    bool isInteger = isIntegerValue
                        ?? (_integerOperandAnalysis?.IsIntegerRegister(newAssignment.Key) == true);
                    // Also before the clear, and not only because it reads better
                    // there: whether the bits are a float's is a question about the
                    // readers as much as the maker, and there are none afterwards.
                    bool isBits = IsBitsVariable(tempValue, isInteger);
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
                    if (existing != null && isIntegerValue != null && existing.IsInteger != isIntegerValue
                        && !tempUsages.Any(u => u is PhiNode))
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
    private static void UnifyBranchVariables(IfStatement ifStatement)
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
    private static void UnifyCaseVariables(SwitchStatement switchStatement)
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
    /// Points every assignment of one variable inside these statements at another,
    /// for a case that carries its result out of a nested statement while an earlier
    /// case already owns the variable the switch will carry.
    /// </summary>
    private static void Reassign(
        IList<IStatement> body, TempVariableNode from, TempVariableNode to)
    {
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
        });
    }

    private void SetReturnStatement(IList<IStatement> statements)
    {
        IStatement lastStatement = statements.Last();

        // These terminate the shader by side effect, so there is nothing to return.
        if (lastStatement is ReturnStatement
            || lastStatement is AppendStatement
            || lastStatement is StoreStructuredStatement
            || lastStatement is AtomicStatement
            || lastStatement is IndexableTempStoreStatement
            || lastStatement is RestartStripStatement
            || lastStatement is SyncStatement)
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
            if (ifStatement.FalseBody != null)
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
            || lastStatement is SwitchStatement)
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
                if (!preLoopStatement.Outputs.TryGetValue(bodyOutput.Key, out var preLoopValue)
                    || preLoopValue is not TempAssignmentNode loopAssignment)
                {
                    continue;
                }

                if (bodyOutput.Value is TempAssignmentNode bodyAssignment)
                {
                    // Not just the property: whatever the body already rewired to read
                    // the old variable goes on reading it, and comes out as a name
                    // nothing ever assigns.
                    ReplaceTempVariable(bodyAssignment.TempVariable, loopAssignment.TempVariable);
                }
                else if (bodyOutput.Value is TempVariableNode joinVariable)
                {
                    // The value came from a branch join rather than a plain assignment.
                    // The join allocated its own variable; it is the loop-carried one.
                    ReplaceTempVariable(joinVariable, loopAssignment.TempVariable);
                }
                else if (bodyOutput.Value is PhiNode joinPhi && !joinPhi.IsLoopHeader)
                {
                    // Same case, but the statement still holds the unlowered join phi.
                    // Lowering the branches rewrote its operands to the variable they
                    // share, so the join variable is reachable through them.
                    foreach (var branchVariable in joinPhi.Inputs.OfType<TempVariableNode>().ToList())
                    {
                        ReplaceTempVariable(branchVariable, loopAssignment.TempVariable);
                    }
                }
            }
        });
    }

    /// <summary>
    /// Rewrites every reference to one temp variable so it uses another, across the
    /// graph and across every statement. <see cref="TempAssignmentNode.TempVariable"/>
    /// is a property rather than a graph input, so it needs its own pass.
    /// </summary>
    private void ReplaceTempVariable(TempVariableNode from, TempVariableNode to)
    {
        if (ReferenceEquals(from, to))
        {
            return;
        }

        from.Replace(to);

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
            if ((statement is StoreStructuredStatement store
                    && (store.Address == node || store.Values.Contains(node)))
                || (statement is StoreTypedStatement typedStore
                    && (typedStore.Coordinates.Contains(node) || typedStore.Values.Contains(node)))
                || (statement is BufferAppendStatement append && append.Values.Contains(node))
                || (statement is AtomicStatement atomic
                    && (atomic.Address == node || atomic.Value == node || atomic.Compare == node
                        || (atomic.Coordinates?.Contains(node) ?? false))))
            {
                holders++;
            }
        });
        return holders > 1 || (holders == 1 && node.Outputs.Count != 0);
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
            else if (node is MoveOperation
                && statement is BreakStatement or ContinueStatement
                && statement.Outputs.Values.Contains(node))
            {
                holders.Add(statement);
            }
        });
        return [.. holders];
    }

    private void ReplaceInStatementNodes(HlslTreeNode node, HlslTreeNode replacement)
    {
        new StatementVisitor(_statements).Visit(statement =>
        {
            if (statement is StoreStructuredStatement store)
            {
                for (int i = 0; i < store.Values.Length; i++)
                {
                    if (store.Values[i] == node)
                    {
                        store.Values[i] = replacement;
                    }
                }
                if (store.Address == node)
                {
                    store.Address = replacement;
                }
            }
            else if (statement is StoreTypedStatement typedStore)
            {
                for (int i = 0; i < typedStore.Values.Length; i++)
                {
                    if (typedStore.Values[i] == node)
                    {
                        typedStore.Values[i] = replacement;
                    }
                }
                for (int i = 0; i < typedStore.Coordinates.Length; i++)
                {
                    if (typedStore.Coordinates[i] == node)
                    {
                        typedStore.Coordinates[i] = replacement;
                    }
                }
            }
            else if (statement is BufferAppendStatement append)
            {
                for (int i = 0; i < append.Values.Length; i++)
                {
                    if (append.Values[i] == node)
                    {
                        append.Values[i] = replacement;
                    }
                }
            }
            else if (statement is AtomicStatement atomic)
            {
                if (atomic.Address == node)
                {
                    atomic.Address = replacement;
                }
                if (atomic.Coordinates != null)
                {
                    for (int i = 0; i < atomic.Coordinates.Length; i++)
                    {
                        if (atomic.Coordinates[i] == node)
                        {
                            atomic.Coordinates[i] = replacement;
                        }
                    }
                }
                if (atomic.Value == node)
                {
                    atomic.Value = replacement;
                }
                if (atomic.Compare == node)
                {
                    atomic.Compare = replacement;
                }
            }
            else if (statement is ClipStatement clip)
            {
                for (int i = 0; i < clip.Values.Length; i++)
                {
                    if (clip.Values[i] == node)
                    {
                        clip.Values[i] = replacement;
                    }
                }
            }
            else if (statement is IndexableTempStoreStatement indexableTempStore)
            {
                for (int i = 0; i < indexableTempStore.Values.Length; i++)
                {
                    if (indexableTempStore.Values[i] == node)
                    {
                        indexableTempStore.Values[i] = replacement;
                    }
                }
                if (indexableTempStore.Index == node)
                {
                    indexableTempStore.Index = replacement;
                }
            }
        });
    }

    /// <summary>
    /// Whether a value is an integer, from the value rather than the register it
    /// was in: fxc reuses a register, and a float4 of texture offsets was declared
    /// int4 for the loop counter that took r0.x over after the loop. What reads
    /// the value decides where the readers agree; the operation that made it
    /// otherwise - an integer add makes an integer, a conversion what it converts
    /// to; and null - the register's own type - where neither says, as for a load,
    /// whose operands are integers whatever it loads, or an immediate nothing reads
    /// as either.
    /// </summary>
    internal static bool? IsIntegerValue(HlslTreeNode value)
    {
        // Bits are an integer and nothing else could be meant, so they are not put
        // to the readers: an integer add of two packed half floats read by a float
        // multiply was typed by the multiply and then computed in floats.
        if (IsBitsValue(value))
        {
            return true;
        }
        if (IsIntegerMadeReadAsFloat(value))
        {
            return true;
        }
        bool? consumedType = InstructionParser.GetConsumedType(value);
        bool? madeType = MadeType(value);
        if (consumedType != null || madeType != null)
        {
            return consumedType ?? madeType;
        }
        // A variable declared integer but read by nothing the graph can see - only by
        // the stores a value leaves a statement through, which are not output edges -
        // still holds an integer. The value an interlocked operation reads out of the
        // resource is one, and stored on into another: left asking its (absent) readers,
        // it printed as a float and the store into it converted back. Left null where
        // nothing says integer, which keeps the float the writers have always written.
        if (value is TempVariableNode variable && variable.IsInteger)
        {
            return true;
        }
        return null;
    }

    /// <summary>
    /// Whether an integer value is unsigned, decided the way its integer-ness is:
    /// by the readers where they agree, by what made it otherwise. ult and ilt both
    /// read as `a &lt; b`, and HLSL takes the signedness from the operands rather
    /// than from the operator, so a value only ever used unsigned is better declared
    /// uint than cast at each use. Null where nothing says either way, which leaves
    /// the int the writer has always declared.
    /// </summary>
    internal static bool? IsUnsignedValue(HlslTreeNode value)
    {
        return ConsumedAsUnsigned(value) ?? MadeUnsigned(value);
    }

    /// <summary>
    /// What the readers of a value agree its signedness is, looking through the
    /// operations that are the same instruction either way - a move, a phi, an add,
    /// a bitwise operator - or null where they disagree or none of them says.
    /// </summary>
    private static bool? ConsumedAsUnsigned(HlslTreeNode value)
    {
        bool? unsigned = null;
        var visited = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>();
        pending.Push(value);
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            foreach (HlslTreeNode reader in node.Outputs)
            {
                if (!visited.Add(reader))
                {
                    continue;
                }
                bool? says = ReadsAsUnsigned(reader, node);
                if (says == null)
                {
                    if (IsSignNeutral(reader, node))
                    {
                        pending.Push(reader);
                    }
                    continue;
                }
                if (unsigned != null && unsigned != says)
                {
                    return null;
                }
                unsigned = says;
            }
        }
        return unsigned;
    }

    /// <summary>Whether this reader of the node reads it as unsigned, or null where
    /// it says nothing about signedness.</summary>
    private static bool? ReadsAsUnsigned(HlslTreeNode reader, HlslTreeNode node)
    {
        switch (reader)
        {
            // ieq and ine are the same comparison either way, so only the ordered
            // ones say anything.
            case ComparisonNode { IsInteger: true, Comparison: IfComparison.LT
                or IfComparison.LE or IfComparison.GT or IfComparison.GE } comparison:
                return comparison.IsUnsigned;
            // What is shifted, not by how much: ushr and ishr differ in what fills
            // the top bits of the value.
            case ShiftRightOperation shift when ReferenceEquals(shift.Value, node):
                return shift.IsUnsigned;
            // umin and umax read both operands as unsigned, imin and imax as signed.
            case MinimumOperation minimum:
                return minimum.IsUnsigned;
            case MaximumOperation maximum:
                return maximum.IsUnsigned;
            // udiv is the only integer divide there is - there is no signed opcode
            // for it - so an integer quotient or remainder is unsigned.
            case DivisionOperation { ConsumesInteger: true }:
            case ModuloOperation { ConsumesInteger: true }:
                return true;
            // ineg, which is meaningless on an unsigned value.
            case NegateOperation when IsIntegerValue(reader) == true:
                return false;
            default:
                return null;
        }
    }

    /// <summary>
    /// Whether the reader is the same instruction signed or unsigned, so that what
    /// reads its result is what knows. imin and umin used to be here because the
    /// parse made one node of both; the node says which now, above.
    /// </summary>
    private static bool IsSignNeutral(HlslTreeNode reader, HlslTreeNode value)
    {
        // A select carries its branches and not its condition: which way the test
        // went says nothing about the number it chose between.
        if (reader is MoveConditionalOperation select)
        {
            return !ReferenceEquals(select.Inputs[0], value);
        }
        return reader is MoveOperation or PhiNode
            or AddOperation or SubtractOperation or MultiplyOperation
            or ShiftLeftOperation
            or BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
            or BitwiseNotOperation;
    }

    /// <summary>What the operation that made a value makes it, signed or unsigned,
    /// or null where the operation does not say.</summary>
    private static bool? MadeUnsigned(HlslTreeNode value)
    {
        return value switch
        {
            // ftou and ftoi.
            ConvertOperation convert => convert.TargetType switch
            {
                "uint" => true,
                "int" => false,
                _ => null,
            },
            ShiftRightOperation shift => shift.IsUnsigned,
            MinimumOperation minimum => minimum.IsUnsigned,
            MaximumOperation maximum => maximum.IsUnsigned,
            // ubfe fills the top of the field with zeroes and ibfe with its sign.
            BitFieldExtractOperation extract => extract.IsUnsigned,
            DivisionOperation { ConsumesInteger: true } => true,
            ModuloOperation { ConsumesInteger: true } => true,
            NegateOperation => false,
            _ => null,
        };
    }

    /// <summary>
    /// What the operation that made a value makes, from that operation alone. Null
    /// where it does not say - a move, a phi, an immediate - which is where the
    /// readers are the only thing that knows.
    /// </summary>
    private static bool? MadeType(HlslTreeNode value)
    {
        return value switch
        {
            ConvertOperation convert => convert.TargetType is "int" or "uint",
            // Conversions too, and for the same reason: what they read and what they
            // make are opposite, so the operation's own ConsumesInteger below would
            // answer the wrong question. f16tof32 unpacking two halves into a
            // float2 had them written as an int2, which truncates them.
            FloatToHalfOperation => true,
            HalfToFloatOperation => false,
            ConstantNode constant => constant.IntegerValue != null,
            // A comparison makes a mask, all ones or all zeroes, which is an
            // integer: a variable holding one beside the -1 the other branch moved
            // in was declared float, and the -1 printed as the NaN its bits are.
            ComparisonNode => true,
            // A byte address buffer hands back uints whatever was stored, and a
            // structured one hands back what its element type says, which is
            // asked of the reflection data elsewhere.
            LoadStructuredNode { IsRaw: true } => true,
            LoadStructuredNode => null,
            // What resinfo and bufinfo report is whatever the instruction asked for
            // them as: a size in texels or a count of elements is a uint, and the
            // same measurement taken as a float is one.
            ResourceInfoNode info => info.ReturnType == D3D10ResInfoReturnType.Uint,
            // A bitwise operator says nothing about what it was given - it carries
            // bits along - but what it makes is an integer whatever went in: HLSL
            // has no other type it could be. Left saying nothing, four masks came
            // out as a float4, and every integer use of them stopped compiling.
            BitwiseAndOperation or BitwiseOrOperation or BitwiseXorOperation
                or BitwiseNotOperation or ShiftLeftOperation or ShiftRightOperation
                or BitFieldExtractOperation or BitFieldInsertOperation => true,
            Operation operation => operation.ConsumesInteger,
            _ => null,
        };
    }

    /// <summary>
    /// Whether an integer value is a float's bits rather than a number. The two
    /// are the same type and want opposite things of a float reader: a loop
    /// counter is converted, a packed pair of half floats is reinterpreted, and
    /// nothing in the type says which.
    ///
    /// Bits enter the graph one way - an integer operation reading a float, which
    /// is where the decompiler writes an asint - and spread from there through the
    /// integer operations that carry them. A constant is not bits: `x &amp; 255` is
    /// bits because x is, not because 255 is.
    /// </summary>
    internal static bool IsBitsValue(HlslTreeNode value)
    {
        return IsBitsValue(value, HlslTreeNode.NewNodeSet());
    }

    /// <summary>
    /// Whether a value standing where a float is wanted is to be reinterpreted
    /// rather than converted: bits, or the dwords a byte address buffer hands back,
    /// which are uints whatever was stored and are read as a float with asfloat.
    /// Converted instead, a float's bits came back as the number they happen to
    /// make. Not bits in the variable's sense - the variable holding them is a
    /// uint, and rightly.
    /// </summary>
    internal static bool IsReinterpretedAsFloat(HlslTreeNode value)
    {
        return value is LoadStructuredNode { IsRaw: true } || IsBitsValue(value);
    }

    private static bool IsBitsValue(HlslTreeNode value, HashSet<HlslTreeNode> visited)
    {
        if (value is TempVariableNode temp)
        {
            return temp.IsBits;
        }
        // Asked of the operation rather than of the readers, so that this can be
        // what the readers are answered with.
        if (MadeType(value) != true || !visited.Add(value))
        {
            return false;
        }
        // Bits enter the graph through a bitwise operator or a shift reading a
        // float, and nowhere else - that read is the asint the writer puts there.
        // An integer add or a conversion reading one converts it, and the integer
        // that comes out is a number: `(int)(16 * x)` is an address, and
        // `3 * n - 7` is arithmetic on a uniform. A constant is never the source
        // of either, whatever type it was given.
        bool reinterprets = value is BitwiseAndOperation or BitwiseOrOperation
            or BitwiseXorOperation or BitwiseNotOperation
            or ShiftLeftOperation or ShiftRightOperation
            or BitFieldExtractOperation or BitFieldInsertOperation;
        foreach (HlslTreeNode input in value.Inputs)
        {
            if (input is ConstantNode)
            {
                continue;
            }
            if ((reinterprets && IsFloatMade(input)) || IsBitsValue(input, visited))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsFloatMade(HlslTreeNode value)
    {
        return value is TempVariableNode temp ? !temp.IsInteger : MadeType(value) == false;
    }

    /// <summary>
    /// Whether the variable a value is assigned to holds bits: it is an integer
    /// variable, and either the value is bits already or it is a float being
    /// reinterpreted on the way in.
    /// </summary>
    internal static bool IsBitsVariable(HlslTreeNode value, bool isInteger)
    {
        return isInteger
            && (IsBitsValue(value) || IsFloatMade(value) || IsIntegerMadeReadAsFloat(value));
    }

    /// <summary>
    /// Integer arithmetic whose result a float operation reads without converting
    /// it. Those bits are a float's: a shader that wanted the number they make
    /// would have put an itof or a utof between, and this one did not. bit_field
    /// assembles a float from a mantissa and an exponent with an iadd and
    /// multiplies what comes out.
    ///
    /// The two questions IsIntegerValue asks disagree here - the maker says
    /// integer and the readers say float - and taking the readers

    /// <summary>
    /// Integer arithmetic whose result a float operation reads without converting
    /// it. Those bits are a float's: a shader that wanted the number they make
    /// would have put an itof or a utof between, and this one did not. bit_field
    /// assembles a float from a mantissa and an exponent with an iadd and
    /// multiplies what comes out.
    ///
    /// The two questions IsIntegerValue asks disagree here - the maker says
    /// integer and the readers say float - and taking the readers' answer turns
    /// the bits into whatever number they happen to be. What tells this apart from
    /// an extracted field, which is a number and is read as one, is that the field
    /// has a conversion in front of it, so its readers say integer too.
    /// </summary>
    private static bool IsIntegerMadeReadAsFloat(HlslTreeNode value)
    {
        return MadeType(value) == true && InstructionParser.GetConsumedType(value) == false;
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

    private void ReplaceAnyAssignment(RegisterComponentKey componentKey, HlslTreeNode node, TempAssignmentNode replacement)
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
