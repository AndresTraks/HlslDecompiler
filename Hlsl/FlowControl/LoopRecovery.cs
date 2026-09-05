using HlslDecompiler.DirectXShaderModel;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// Turns the shape a bytecode loop actually has
///
/// <code>
/// t = init;
/// while (true) {
///     if (t &gt;= limit) { break; }
///     ...
///     t = t + step;
/// }
/// </code>
///
/// back into the <c>for</c> loop it was written as. Only unbounded loops are
/// considered - a D3D9 <c>rep</c> already carries its own trip count, and folding a
/// second condition into it would not be an improvement.
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

        // In a for loop `continue` runs the increment; in the bytecode it jumps past
        // it. Rewriting a loop that contains one would change what it computes.
        if (ContainsContinue(loop.Body))
        {
            return;
        }

        // The guard is the first statement that branches. Assignments may precede it -
        // they compute the comparison, which becomes part of the for condition.
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

        // Work this out before anything is moved: not every comparison can be
        // inverted, and the statements must not be left half-rewritten.
        ComparisonNode continueCondition = Invert(breakCondition);
        if (continueCondition == null)
        {
            return;
        }

        if (loop.Body[loop.Body.Count - 1] is not AssignmentStatement lastStatement)
        {
            return;
        }

        // The increment must be `v = v + step` on a variable the break tests.
        var increment = lastStatement.Outputs
            .Where(o => o.Value is TempAssignmentNode assignment
                && assignment.Value is AddOperation add
                && add.Inputs.Any(a => ReferenceEquals(a, assignment.TempVariable))
                && add.Inputs.Any(a => a is ConstantNode))
            .Select(o => (Key: o.Key, Assignment: (TempAssignmentNode)o.Value))
            .FirstOrDefault(o => IsTestedBy(o.Assignment.TempVariable, breakCondition));
        if (increment.Assignment == null)
        {
            return;
        }

        TempVariableNode inductionVariable = increment.Assignment.TempVariable;

        // The initial value has to be an assignment to the same variable immediately
        // before the loop, and nothing after the loop may still read it - moving the
        // declaration into the for header narrows its scope to the loop.
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

        // Those three now live in the for header rather than in the body.
        initializerStatement.Outputs.Remove(increment.Key);
        lastStatement.Outputs.Remove(increment.Key);
        loop.Body.RemoveAt(guardIndex);
    }

    /// <summary>
    /// The condition under which a statement leaves the loop, or null if it is not a
    /// guard. Bytecode spells this either as a conditional break (<c>breakc</c>) or
    /// as an <c>if</c> wrapping an unconditional one, depending on how it was
    /// compiled.
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

    /// <summary>The loop runs while the break condition does not hold.</summary>
    private static ComparisonNode Invert(ComparisonNode comparison)
    {
        IfComparison inverted = comparison.Comparison switch
        {
            IfComparison.GT => IfComparison.LE,
            IfComparison.GE => IfComparison.LT,
            IfComparison.LT => IfComparison.GE,
            IfComparison.LE => IfComparison.GT,
            IfComparison.EQ => IfComparison.NE,
            IfComparison.NE => IfComparison.EQ,
            _ => IfComparison.None,
        };
        return inverted == IfComparison.None
            ? null
            : new ComparisonNode(comparison.Left, comparison.Right, inverted);
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
            foreach (HlslTreeNode value in statement.Outputs.Values.Concat(statement.Inputs.Values))
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
