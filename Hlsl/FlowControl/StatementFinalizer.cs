using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl
{
    public class StatementFinalizer
    {
        private IList<IStatement> _statements;

        private StatementFinalizer(IList<IStatement> statements)
        {
            _statements = statements;
        }

        public static void Finalize(IList<IStatement> statements)
        {
            var finalizer = new StatementFinalizer(statements);
            finalizer.FinalizeStatements();
        }

        private void FinalizeStatements()
        {
            RemoveUnusedAssignmentInputOutput();
            RemoveUnusedAssignments(_statements);
            InsertTempVariableAssignments(_statements);
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

                if (statement is AssignmentStatement assignment)
                {
                    outputsToRemove = statement.Outputs
                        .Where(o => o.Value is PhiNode && o.Value.Outputs.Count == 0)
                        .ToList();
                    foreach (var output in outputsToRemove)
                    {
                        statement.Outputs.Remove(output.Key);
                        output.Value.Remove();
                    }
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

                    // Check if assignment output goes only into itself
                    if (assignmentNode.Outputs.All(v => v.IsInputOf(assignment.Outputs.Values)))
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
                            if (assignmentNode.IsInputOf(ifStatement.Comparison))
                            {
                                // TODO:
                                /*
                                assignment.Outputs.Remove(assignmentOutput.Key);
                                ifStatement.Inputs.Remove(assignmentOutput.Key);
                                if (ifStatement.Outputs.TryGetValue(assignmentOutput.Key, out var ifOutput) && ifOutput == assignmentNode)
                                {
                                    ifStatement.Outputs.Remove(assignmentOutput.Key);
                                }
                                */
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
                    if (doesOutputExitStatement || tempInputAssignment != null)
                    {
                        List<HlslTreeNode> tempUsages = tempValue.Outputs.ToList();
                        tempValue.Outputs.Clear();
                        TempVariableNode tempVariable = tempInputAssignment != null
                            ? tempInputAssignment.TempVariable
                            : new TempVariableNode();
                        var tempAssignment = new TempAssignmentNode(tempVariable, tempValue);
                        if (tempUsages.All(u => u is PhiNode) || tempInputAssignment != null)
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
                            }
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

                    foreach (var trueTempAssignment in ifStatement.TrueBody.Last().Outputs.Where(o => o.Value is TempAssignmentNode))
                    {
                        if (ifStatement.FalseBody.Last().Outputs.TryGetValue(trueTempAssignment.Key, out var falseAssignment))
                        {
                            if (falseAssignment is TempAssignmentNode falseTempAssignment)
                            {
                                TempAssignmentNode trueValue = trueTempAssignment.Value as TempAssignmentNode;
                                //falseTempAssignment.TempVariable.Replace(trueValue.TempVariable);
                                falseTempAssignment.TempVariable = trueValue.TempVariable;
                                ifStatement.Outputs[trueTempAssignment.Key] = trueValue.TempVariable;
                            }
                        }
                    }
                }
            }
            else if (statement is LoopStatement loopStatement)
            {
                InsertTempVariableAssignments(loopStatement.Body);

                foreach (var loopBodyAssignment in loopStatement.Body.Last().Outputs.Where(o => o.Value is TempAssignmentNode).ToList())
                {
                    if (i >= 1 && statements[i - 1] is AssignmentStatement loopAssignmentStatement)
                    {
                        var loopAssignment = loopAssignmentStatement.Outputs[loopBodyAssignment.Key] as TempAssignmentNode;
                        TempAssignmentNode loopBodyAssignmentNode = loopBodyAssignment.Value as TempAssignmentNode;
                        //loopBodyAssignmentNode.TempVariable.Replace(loopAssignment.TempVariable);
                        loopBodyAssignmentNode.TempVariable = loopAssignment.TempVariable;
                    }
                }
            }
        }

        private static void SetReturnStatement(IList<IStatement> statements)
        {
            IStatement lastStatement = statements.Last();
            if (lastStatement is ReturnStatement)
            {
                return;
            }
            if (lastStatement is AssignmentStatement assignment)
            {
                statements[statements.Count - 1] = new ReturnStatement(assignment.Outputs);
                return;
            }
            if (lastStatement is IfStatement ifStatement)
            {
                SetReturnStatement(ifStatement.TrueBody);
                if (ifStatement.FalseBody != null)
                {
                    SetReturnStatement(ifStatement.FalseBody);
                }
                return;
            }
            throw new NotImplementedException();
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
}
