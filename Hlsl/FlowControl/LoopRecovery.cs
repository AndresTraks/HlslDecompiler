using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// Turns <c>t = init; while (true) { if (t &gt;= limit) break; ...; t += step; }</c>
/// back into a <c>for</c> loop. Only unbounded loops: a <c>rep</c> has its own count.
/// </summary>
public static class LoopRecovery
{
    public static void Recover(IList<IStatement> statements)
    {
        for (int i = 0; i < statements.Count; i++)
        {
            switch (statements[i])
            {
                case LoopStatement loop:
                    Recover(loop.Body);
                    TryRecoverForLoop(statements, i, loop);
                    break;
                case IfStatement ifStatement:
                    Recover(ifStatement.TrueBody);
                    if (ifStatement.FalseBody != null)
                    {
                        Recover(ifStatement.FalseBody);
                    }
                    break;
                case SwitchStatement switchStatement:
                    foreach (SwitchCase switchCase in switchStatement.Cases)
                    {
                        Recover(switchCase.Body);
                    }
                    break;
            }
        }
    }

    private static void TryRecoverForLoop(IList<IStatement> statements, int index, LoopStatement loop)
    {
        if (loop.RepeatCount != null || loop.Body.Count < 2 || index < 1)
        {
            return;
        }

        // A for loop's `continue` runs the increment; the bytecode's skips it.
        if (ContainsContinue(loop.Body))
        {
            return;
        }

        // Assignments before the guard compute its comparison.
        int guardIndex = 0;
        while (guardIndex < loop.Body.Count && loop.Body[guardIndex] is AssignmentStatement)
        {
            guardIndex++;
        }
        if (guardIndex >= loop.Body.Count
            || TryGetBreakCondition(loop.Body[guardIndex]) is not ComparisonNode breakCondition)
        {
            return;
        }

        // Before anything moves: not every comparison inverts.
        ComparisonNode continueCondition = breakCondition.Inverted();
        if (continueCondition == null)
        {
            return;
        }

        if (loop.Body[loop.Body.Count - 1] is not AssignmentStatement lastStatement)
        {
            return;
        }

        // `v = v op const` (add, shift or multiply) on a variable the break tests.
        var increment = lastStatement.Outputs
            .Where(o => o.Value is TempAssignmentNode assignment
                && assignment.Value is AddOperation or ShiftRightOperation or ShiftLeftOperation or MultiplyOperation
                && assignment.Value.Inputs.Any(a => ReferenceEquals(a, assignment.TempVariable))
                && assignment.Value.Inputs.Any(a => a is ConstantNode))
            .Select(o => (Key: o.Key, Assignment: (TempAssignmentNode)o.Value))
            .FirstOrDefault(o => IsTestedBy(o.Assignment.TempVariable, breakCondition));
        // fxc may compute the step early (`w = v + 1`, fused into another iadd) and
        // end the body on `v = w`; that is still a for loop.
        AssignmentStatement stepStatement = null;
        RegisterComponentKey stepKey = default;
        if (increment.Assignment == null)
        {
            foreach (var copy in lastStatement.Outputs)
            {
                if (copy.Value is not TempAssignmentNode copied
                    || CopiedVariable(copied.Value) is not TempVariableNode stepVariable
                    || !IsTestedBy(copied.TempVariable, breakCondition))
                {
                    continue;
                }
                foreach (IStatement statement in loop.Body)
                {
                    // The step can be in the copy's own statement (one iadd writes both).
                    if (statement is not AssignmentStatement step)
                    {
                        continue;
                    }
                    var stepped = step.Outputs.FirstOrDefault(o =>
                        !ReferenceEquals(o.Value, copied)
                        && o.Value is TempAssignmentNode assignment
                        && ReferenceEquals(assignment.TempVariable, stepVariable)
                        && assignment.Value is AddOperation or ShiftRightOperation
                            or ShiftLeftOperation or MultiplyOperation
                        && assignment.Value.Inputs.Any(a => a is ConstantNode)
                        && assignment.Value.Inputs.Any(a =>
                            ReferenceEquals(a, copied.TempVariable) || ReferenceEquals(a, stepVariable)));
                    if (stepped.Value == null
                        || IsReadElsewhere(loop.Body, stepVariable, copied))
                    {
                        continue;
                    }
                    // The counter takes the step expression; the temp register goes.
                    increment = (copy.Key, new TempAssignmentNode(
                        copied.TempVariable, ((TempAssignmentNode)stepped.Value).Value)
                    {
                        IsReassignment = true,
                    });
                    stepStatement = step;
                    stepKey = stepped.Key;
                    break;
                }
                if (stepStatement != null)
                {
                    break;
                }
            }
        }
        if (increment.Assignment == null)
        {
            return;
        }

        TempVariableNode inductionVariable = increment.Assignment.TempVariable;

        // Initialized right before the loop and unread after it: the for header
        // narrows its scope.
        if (statements[index - 1] is not AssignmentStatement initializerStatement
            || !initializerStatement.Outputs.TryGetValue(increment.Key, out var initialValue)
            || initialValue is not TempAssignmentNode initializer
            || !ReferenceEquals(initializer.TempVariable, inductionVariable)
            || initializer.IsReassignment)
        {
            return;
        }
        if (IsUsedAfter(statements, index, inductionVariable))
        {
            return;
        }

        loop.Initializer = initializer;
        loop.ContinueCondition = continueCondition;
        loop.Increment = increment.Assignment;

        initializerStatement.Outputs.Remove(increment.Key);
        lastStatement.Outputs.Remove(increment.Key);
        stepStatement?.Outputs.Remove(stepKey);
        loop.Body.RemoveAt(guardIndex);
    }

    /// <summary>
    /// The exit condition of a <c>breakc</c> or an <c>if</c> around a <c>break</c>.
    /// </summary>
    private static HlslTreeNode TryGetBreakCondition(IStatement statement)
    {
        if (statement is BreakStatement conditionalBreak)
        {
            return conditionalBreak.Comparison;
        }

        if (statement is IfStatement ifStatement
            && ifStatement.FalseBody == null
            && ifStatement.TrueBody.Count == 1
            && ifStatement.TrueBody[0] is BreakStatement { Comparison: null }
            && ifStatement.Comparison.Length == 1)
        {
            return ifStatement.Comparison[0];
        }

        return null;
    }

    private static bool IsTestedBy(TempVariableNode variable, ComparisonNode comparison)
    {
        return variable != null
            && (ReferenceEquals(comparison.Left, variable) || ReferenceEquals(comparison.Right, variable));
    }

    private static bool ContainsContinue(IList<IStatement> statements)
    {
        bool found = false;
        new StatementVisitor(statements).Visit(statement =>
        {
            if (statement is ContinueStatement)
            {
                found = true;
            }
        });
        return found;
    }



    private static TempVariableNode CopiedVariable(HlslTreeNode value)
    {
        return value switch
        {
            TempVariableNode variable => variable,
            MoveOperation move => move.Inputs[0] as TempVariableNode,
            _ => null,
        };
    }

    /// <summary>
    /// Whether anything but the copy reads the step's register, which must then stay.
    /// What a statement holds counts: an atomic's operands are in no register map.
    /// </summary>
    private static bool IsReadElsewhere(
        IList<IStatement> body, TempVariableNode variable, TempAssignmentNode copy)
    {
        bool read = false;
        new StatementVisitor(body).Visit(statement =>
        {
            foreach (HlslTreeNode value in statement.Outputs.Values.Concat(statement.Inputs.Values)
                .Concat(statement.HeldNodes))
            {
                if (ReferenceEquals(value, copy))
                {
                    continue;
                }
                if (ReferenceEquals(value, variable) || variable.IsInputOf(value))
                {
                    read = true;
                }
            }
        });
        return read;
    }

    private static bool IsUsedAfter(IList<IStatement> statements, int loopIndex, TempVariableNode variable)
    {
        var following = statements.Skip(loopIndex + 1).ToList();
        if (following.Count == 0)
        {
            return false;
        }

        bool used = false;
        new StatementVisitor(following).Visit(statement =>
        {
            foreach (HlslTreeNode value in statement.Outputs.Values.Concat(statement.Inputs.Values)
                .Concat(statement.HeldNodes))
            {
                if (ReferenceEquals(value, variable) || variable.IsInputOf(value))
                {
                    used = true;
                }
            }
        });
        return used;
    }
}
