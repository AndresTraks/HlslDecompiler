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
        LoopRecovery.Recover(_statements);
        SetReturnStatement(_statements);
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
            var assignmentOutputs = assignment.Outputs.Where(o => o.Key.RegisterKey.IsTempRegister).ToDictionary();
            foreach (var assignmentOutput in assignmentOutputs)
            {
                var assignmentNode = assignmentOutput.Value;

                // Check if assignment output goes only into itself. A phi consumer
                // means the value leaves the statement - to a branch join, or along a
                // loop backedge into the next iteration - so it is still live.
                if (assignmentNode.Outputs.All(v => v is not PhiNode && v.IsInputOf(assignment.Outputs.Values)))
                {
                    RemoveAnyAssignment(assignmentNode);
                    continue;
                }

                // Check if assignment output goes only into the next statement
                if (i < statements.Count - 1)
                {
                    IStatement nextStatement = statements[i + 1];
                    if (nextStatement is ClipStatement clip)
                    {
                        if (assignmentNode.IsInputOf(clip.Values))
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
                            && assignmentNode.Outputs.All(o => o.IsInputOf(ifStatement.Comparison)))
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
            foreach (var newAssignment in newAssignments)
            {
                HlslTreeNode tempValue = newAssignment.Value;

                // Insert temp variable if value has output outside of current statement
                // or if an iteration variable is changed
                bool doesOutputExitStatement = tempValue.Outputs.Any(v => !v.IsInputOf(statement.Outputs.Values));
                statement.Inputs.TryGetValue(newAssignment.Key, out var inputAssignment);
                var tempInputAssignment = inputAssignment as TempAssignmentNode;
                TempVariableNode tempInputVariable = GetExistingVariable(inputAssignment);
                if (doesOutputExitStatement || tempInputAssignment != null)
                {
                    List<HlslTreeNode> tempUsages = tempValue.Outputs.ToList();
                    tempValue.Outputs.Clear();
                    TempVariableNode tempVariable = tempInputAssignment?.TempVariable
                        ?? tempInputVariable
                        ?? new TempVariableNode
                        {
                            IsInteger = _integerOperandAnalysis?.IsIntegerRegister(newAssignment.Key) == true,
                        };
                    var tempAssignment = new TempAssignmentNode(tempVariable, tempValue);
                    // The value entering a loop header declares the variable; everything
                    // else that feeds a phi - a branch join, or the loop backedge - is
                    // assigning to a variable that already exists.
                    bool declaresLoopVariable = tempUsages.Count != 0
                        && tempUsages.All(u => u is PhiNode phi
                            && phi.IsLoopHeader
                            && ReferenceEquals(phi.PreLoopValue, tempValue));
                    if ((tempUsages.All(u => u is PhiNode) && !declaresLoopVariable)
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
                            ReplaceInStatementNodes(tempUsage, tempVariable);
                        }
                        // Keep the back-reference, so that rewiring the variable later -
                        // unifying the branches of an if onto one variable, say - can find
                        // the uses, the merging phi among them.
                        tempVariable.Outputs.Add(tempUsage);
                        int index = tempUsage.Inputs.IndexOf(tempValue);
                        tempUsage.Inputs[index] = tempVariable;
                    }
                    ReplaceAnyAssignment(newAssignment.Key, tempValue, tempAssignment);
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
        // which the backedge handling below already accounts for.
        if (inputAssignment is PhiNode phi
            && !phi.IsLoopHeader
            && phi.Inputs.Count != 0
            && phi.Inputs[0] is TempVariableNode merged
            && phi.Inputs.All(i => ReferenceEquals(i, merged)))
        {
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

        foreach (var entry in variableByRegister)
        {
            switchStatement.Outputs[entry.Key] = entry.Value;
        }
    }

    private void SetReturnStatement(IList<IStatement> statements)
    {
        IStatement lastStatement = statements.Last();

        // These terminate the shader by side effect, so there is nothing to return.
        if (lastStatement is ReturnStatement
            || lastStatement is AppendStatement
            || lastStatement is StoreStructuredStatement
            || lastStatement is RestartStripStatement)
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
                    bodyAssignment.TempVariable = loopAssignment.TempVariable;
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
        });
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
