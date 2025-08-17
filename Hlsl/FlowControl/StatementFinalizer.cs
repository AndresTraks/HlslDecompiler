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

        public static void Optimize(IList<IStatement> statements)
        {
            var optimizer = new StatementFinalizer(statements);
            optimizer.OptimizeStatements();
        }

        private void OptimizeStatements()
        {
            RemovePhiNodes(_statements);
            RemoveAssignments(_statements);
            InsertTempVariables(_statements);
            SetReturnStatement(_statements);
        }

        private static void RemovePhiNodes(IList<IStatement> statements)
        {
            for (int i = 0; i < statements.Count; i++)
            {
                RemovePhiNodes(statements, i);
            }
        }

        private static void RemovePhiNodes(IList<IStatement> statements, int i)
        {
            var statement = statements[i];
            if (statement is AssignmentStatement assignment)
            {
                var outputsToRemove = statement.Outputs
                    .Where(o => o.Value is PhiNode && o.Value.Outputs.Count == 0)
                    .ToList();
                foreach (var output in outputsToRemove)
                {
                    statement.Outputs.Remove(output.Key);
                    output.Value.Remove();
                }
            }
            else if (statements[i] is IfStatement ifStatement)
            {
                RemovePhiNodes(ifStatement.TrueBody);
                if (ifStatement.FalseBody != null)
                {
                    RemovePhiNodes(ifStatement.FalseBody);
                }
            }
            else if (statements[i] is LoopStatement loopStatement)
            {
                RemovePhiNodes(loopStatement.Body);
            }
        }

        private void RemoveAssignments(IList<IStatement> statements)
        {
            for (int i = 0; i < statements.Count; i++)
            {
                RemoveAssignments(statements, i);
            }
        }

        private void RemoveAssignments(IList<IStatement> statements, int i)
        {
            if (statements[i] is AssignmentStatement assignment)
            {
                var assignmentOutputs = assignment.Outputs.Where(o => o.Key.RegisterKey.IsTempRegister).ToDictionary();
                foreach (var assignmentOutput in assignmentOutputs)
                {
                    var assignmentNode = assignmentOutput.Value;

                    // Check if assignment output goes only into itself
                    if (assignmentNode.Outputs.All(v => v.IsInputOf(assignmentOutputs.Values)))
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
                            if (assignmentNode.IsInputOf(clip.Value.Inputs))
                            {
                                assignment.Outputs.Remove(assignmentOutput.Key);
                                clip.Inputs.Remove(assignmentOutput.Key);
                            }
                        }
                        else if (nextStatement is IfStatement ifStatement)
                        {
                            if (assignmentNode.IsInputOf(ifStatement.Comparison.Inputs))
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
                RemoveAssignments(ifStatement.TrueBody);
                if (ifStatement.FalseBody != null)
                {
                    RemoveAssignments(ifStatement.FalseBody);
                }
            }
        }

        private static void InsertTempVariables(IList<IStatement> statements)
        {
            foreach (var statement in statements)
            {
                var newAssignments = statement.Outputs.Where(o => o.Key.RegisterKey.IsTempRegister && !statement.Inputs.ContainsKey(o.Key)).ToList();
                foreach (var newAssignment in newAssignments)
                {
                    HlslTreeNode tempInput = newAssignment.Value;

                    if (statement is AssignmentStatement)
                    {
                        if (tempInput.Outputs.Any(v => !v.IsInputOf(statement.Outputs.Values)))
                        {
                            List<HlslTreeNode> tempUsages = tempInput.Outputs.ToList();
                            tempInput.Outputs.Clear();
                            TempVariableNode tempVariable = new(newAssignment.Key);
                            var tempAssignment = new TempAssignmentNode(tempVariable, tempInput);
                            if (tempUsages.All(u => u is PhiNode))
                            {
                                tempAssignment.IsReassignment = true;
                            }
                            foreach (var tempUsage in tempUsages)
                            {
                                int index = tempUsage.Inputs.IndexOf(tempInput);
                                if (tempUsage is PhiNode)
                                {
                                    foreach (var output in tempUsage.Outputs)
                                    {
                                        for (int i = 0; i < output.Inputs.Count; i++)
                                        {
                                            if (output.Inputs[i] == tempUsage)
                                            {
                                                output.Inputs[i] = tempVariable;
                                            }
                                        }
                                        tempVariable.Outputs.Add(output);
                                    }
                                }
                                tempUsage.Inputs[index] = tempVariable;
                                statement.Outputs[newAssignment.Key] = tempAssignment;
                            }
                        }
                    }
                }

                if (statement is IfStatement ifStatement)
                {
                    InsertTempVariables(ifStatement.TrueBody);
                    if (ifStatement.FalseBody != null)
                    {
                        InsertTempVariables(ifStatement.FalseBody);
                    }

                    foreach (var trueTempAssignment in ifStatement.TrueBody.Last().Outputs.Where(o => o.Value is TempAssignmentNode))
                    {
                        var falseTempAssignment = ifStatement.FalseBody.Last().Outputs[trueTempAssignment.Key] as TempAssignmentNode;
                        if (falseTempAssignment != null)
                        {
                            TempAssignmentNode trueValue = trueTempAssignment.Value as TempAssignmentNode;
                            falseTempAssignment.TempVariable.Replace(trueValue.TempVariable);
                            falseTempAssignment.TempVariable = trueValue.TempVariable;
                            ifStatement.Outputs[trueTempAssignment.Key] = trueValue.TempVariable;
                        }
                    }
                }
                else if (statement is LoopStatement loopStatement)
                {
                    InsertTempVariables(loopStatement.Body);
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
    }
}
