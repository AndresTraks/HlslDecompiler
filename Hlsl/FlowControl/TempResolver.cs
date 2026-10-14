using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// Lets templates see through a variable to its value, e.g. a reciprocal fxc hoisted
/// into a temp. Only for a variable written once at top level, read by no phi, whose
/// value reads nothing an intervening statement could change.
/// </summary>
public class TempResolver
{
    // Guard against an accidentally cyclic graph.
    private const int MaxDepth = 100;

    // The assignment is what goes dead when a fold leaves the variable unread.
    private readonly Dictionary<TempVariableNode, HlslTreeNode> _values =
        new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<TempVariableNode, TempAssignmentNode> _assignments =
        new(ReferenceEqualityComparer.Instance);

    public static TempResolver Build(IList<IStatement> statements)
    {
        var resolver = new TempResolver();
        resolver.FindReadable(statements);
        return resolver;
    }

    private void FindReadable(IList<IStatement> statements)
    {
        // A second write anywhere spoils a variable.
        var writes = new Dictionary<TempVariableNode,
            List<(IStatement Statement, TempAssignmentNode Assignment)>>(
                ReferenceEqualityComparer.Instance);
        new StatementVisitor(statements).Visit(statement =>
        {
            foreach (TempAssignmentNode assignment in WritesOf(statement))
            {
                if (!writes.TryGetValue(assignment.TempVariable, out var list))
                {
                    writes[assignment.TempVariable] = list = [];
                }
                list.Add((statement, assignment));
            }
        });
        HashSet<IStatement> topLevel = [.. statements];

        // One write, at top level so every read follows it, and no phi reading it.
        var eligible = new List<(TempVariableNode Variable, TempAssignmentNode Assignment)>();
        foreach ((TempVariableNode variable,
                List<(IStatement Statement, TempAssignmentNode Assignment)> variableWrites)
            in writes)
        {
            if (variableWrites.Count != 1)
            {
                continue;
            }
            (IStatement statement, TempAssignmentNode assignment) = variableWrites[0];
            if (!topLevel.Contains(statement)
                || variable.Outputs.Any(output => output is PhiNode))
            {
                continue;
            }
            eligible.Add((variable, assignment));
        }

        // Fixed point: a value may read only variables that have already joined.
        bool joined;
        do
        {
            joined = false;
            foreach ((TempVariableNode variable, TempAssignmentNode assignment) in eligible)
            {
                if (!_values.ContainsKey(variable) && IsReadable(assignment.Value, _values.ContainsKey))
                {
                    _values[variable] = assignment.Value;
                    _assignments[variable] = assignment;
                    joined = true;
                }
            }
        }
        while (joined);
    }

    private static IEnumerable<TempAssignmentNode> WritesOf(IStatement statement)
    {
        foreach ((RegisterComponentKey key, HlslTreeNode value) in statement.Outputs)
        {
            if (value is not TempAssignmentNode assignment)
            {
                continue;
            }
            // A carried value is not a write; otherwise every loop-invariant variable
            // looks loop-carried.
            if (IsCarried(statement, key, assignment))
            {
                continue;
            }
            yield return assignment;
        }
        if (statement is LoopStatement loop)
        {
            if (loop.Initializer != null)
            {
                yield return loop.Initializer;
            }
            if (loop.Increment != null)
            {
                yield return loop.Increment;
            }
        }
    }

    // Safe to re-evaluate at the read: arithmetic yes; not a load (a store between
    // changes it, and it costs a second load), an attribute evaluation, or a phi.
    private static bool IsReadable(HlslTreeNode node, Func<TempVariableNode, bool> isReadable)
    {
        switch (node)
        {
            case PhiNode:
                return false;
            case TempVariableNode variable:
                return isReadable(variable);
            case RegisterInputNode:
            case ConstantNode:
            case DoubleConstantNode:
                return true;
            case Operation operation
                when operation is not LoadStructuredNode and not EvaluateAttributeOperation:
                return operation.Inputs.All(input => IsReadable(input, isReadable));
            default:
                return false;
        }
    }

    /// <summary>
    /// The variable's value, following chains; otherwise the node itself.
    /// </summary>
    public HlslTreeNode Resolve(HlslTreeNode node)
    {
        for (int depth = 0; depth < MaxDepth
            && node is TempVariableNode variable
            && _values.TryGetValue(variable, out HlslTreeNode value);
            depth++)
        {
            node = value;
        }
        return node;
    }

    /// <summary>
    /// Removes assignments a fold left unread. The variable's Outputs still list the
    /// replaced nodes, so liveness is asked from what statements write instead.
    /// </summary>
    public void RemoveUnreadAssignments(IList<IStatement> statements)
    {
        // Repeat: removing one dead assignment can leave another unread.
        bool removed;
        do
        {
            removed = RemoveUnreadAssignments(statements, statements);
        }
        while (removed);
    }

    // Liveness always needs the whole function, hence `all`.
    private bool RemoveUnreadAssignments(IList<IStatement> statements, IList<IStatement> all)
    {
        bool removed = false;
        for (int i = statements.Count - 1; i >= 0; i--)
        {
            IStatement statement = statements[i];
            int outputsBefore = statement.Outputs.Count;
            foreach ((RegisterComponentKey key, HlslTreeNode value) in statement.Outputs
                .Where(o => IsCandidate(statement, o.Key, o.Value))
                .ToList())
            {
                if (IsLive((TempAssignmentNode)value, all))
                {
                    continue;
                }
                statement.Outputs.Remove(key);
                removed = true;
            }
            if (statement is AssignmentStatement && outputsBefore != 0 && statement.Outputs.Count == 0)
            {
                // Only an assignment statement is empty without outputs; stores and
                // atomics still have their effect.
                statements.RemoveAt(i);
                continue;
            }
            switch (statement)
            {
                case IfStatement ifStatement:
                    removed |= RemoveUnreadAssignments(ifStatement.TrueBody, all);
                    if (ifStatement.FalseBody != null)
                    {
                        removed |= RemoveUnreadAssignments(ifStatement.FalseBody, all);
                    }
                    break;
                case LoopStatement loopStatement:
                    removed |= RemoveUnreadAssignments(loopStatement.Body, all);
                    break;
                case SwitchStatement switchStatement:
                    foreach (SwitchCase switchCase in switchStatement.Cases)
                    {
                        removed |= RemoveUnreadAssignments(switchCase.Body, all);
                    }
                    break;
            }
        }
        return removed;
    }

    private bool IsCandidate(IStatement statement, RegisterComponentKey key, HlslTreeNode value)
    {
        return value is TempAssignmentNode assignment
            && !IsCarried(statement, key, value)
            && ReferenceEquals(_assignments.GetValueOrDefault(assignment.TempVariable), assignment);
    }

    // Whether anything the function writes reaches the variable, starting from every
    // root except the assignment itself. Its value counts too: a vector value shared
    // with a sibling component that is still written keeps it.
    private bool IsLive(TempAssignmentNode assignment, IList<IStatement> statements)
    {
        HashSet<HlslTreeNode> written = HlslTreeNode.NewNodeSet();
        var stack = new Stack<HlslTreeNode>();
        new StatementVisitor(statements).Visit(statement =>
        {
            foreach (HlslTreeNode root in Roots(statement))
            {
                if (!ReferenceEquals(root, assignment))
                {
                    stack.Push(root);
                }
            }
        });
        while (stack.Count != 0)
        {
            HlslTreeNode node = stack.Pop();
            if (!written.Add(node))
            {
                continue;
            }
            // Not TraversableInputs: a phi's backedge value is still read by the loop.
            foreach (HlslTreeNode input in node.Inputs)
            {
                stack.Push(input);
            }
        }
        return written.Contains(assignment.TempVariable)
            || written.Contains(assignment.Value)
            || assignment.TempVariable.Outputs.Any(reader => written.Contains(reader))
            // A held statement keeps every component of the instruction it names.
            || SameInstructionSharedWithHoldingStatement(assignment, statements);
    }

    // The finalizer's same-instruction rule, re-asked after lowering of values,
    // variables and sibling components.
    private static bool SameInstructionSharedWithHoldingStatement(
        TempAssignmentNode assignment, IList<IStatement> statements)
    {
        if (assignment.Value.SourceInstruction == 0)
        {
            return false;
        }
        var components = new List<TempAssignmentNode> { assignment };
        new StatementVisitor(statements).Visit(statement =>
        {
            components.AddRange(statement.Outputs.Values.OfType<TempAssignmentNode>()
                .Where(a => HlslTreeNode.IsSameInstruction(a.Value, assignment.Value)));
        });
        bool shared = false;
        new StatementVisitor(statements).Visit(statement =>
        {
            shared |= statement.NamedHeldNodes.Any(node => components.Any(component =>
                ReferenceEquals(node, component.Value)
                || ReferenceEquals(node, component.TempVariable)));
        });
        return shared;
    }

    // Handed in unchanged; the writer skips it with the same test.
    private static bool IsCarried(IStatement statement, RegisterComponentKey key, HlslTreeNode value)
    {
        return statement.Inputs.TryGetValue(key, out HlslTreeNode carried)
            && ReferenceEquals(carried, value);
    }

    /// <summary>
    /// The values a statement renders: its non-carried outputs and held nodes.
    /// </summary>
    private static IEnumerable<HlslTreeNode> Roots(IStatement statement)
    {
        switch (statement)
        {
            case AssignmentStatement assignment:
                foreach ((RegisterComponentKey key, HlslTreeNode value) in assignment.Outputs)
                {
                    if (IsCarried(assignment, key, value))
                    {
                        continue;
                    }
                    yield return value;
                }
                break;
            case ReturnStatement returnValue:
                // Carried entries too: the return prints them.
                foreach (HlslTreeNode value in returnValue.Outputs.Values)
                {
                    yield return value;
                }
                break;
        }
        foreach (HlslTreeNode held in statement.HeldNodes)
        {
            yield return held;
        }
    }
}
