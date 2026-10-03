using HlslDecompiler.DirectXShaderModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// The values a template can see through a variable to.
///
/// A template matches an expression, and a variable is a leaf of one: a reciprocal
/// that fxc hoisted into a loop-invariant temp has stopped being the reciprocal of a
/// division, and the folds built on that - a multiply by a reciprocal is a divide,
/// and a fraction of one put back with its sign is a remainder - do not reassemble
/// across the name. A variable that is written once at the top of the function, that
/// no phi reads, and whose value reads nothing a statement in between could have
/// changed, holds its expression wherever it is read; the fold can be handed that.
/// </summary>
public class TempResolver
{
    // A chain of variables pointing at variables is walked this deep and no further.
    // The graph is acyclic, so the walk ends without the guard; the guard is there
    // for a graph that has forgotten to be.
    private const int MaxDepth = 100;

    // The expression each variable was seen to hold, and the one assignment that
    // wrote it - the assignment that goes dead when a fold leaves the variable
    // unread, which is what RemoveUnreadAssignments takes out.
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
        // Every write of every variable, wherever in the function it is: a second
        // write spoils a variable no matter where it hides.
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

        // What a variable has to survive being seen through: one write, so that
        // there is one value to read; that write at the top of the function, so that
        // every read follows it and no branch can leave the register holding its old
        // value where the expression would be read instead; and no phi reading it,
        // since a phi is where the value comes round a loop, or out of a branch that
        // did not run, and after one the variable is not that expression any more.
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

        // And what its value has to be - joined once every variable the value reads
        // has joined before it, so that what gets inlined reads nothing whose value
        // the read point cannot vouch for.
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
            // A statement handed a register it does not change carries that value's
            // assignment through its outputs without assigning it - the writer skips
            // it too, and counting it as a write made every loop-invariant variable
            // look loop-carried.
            if (IsCarried(statement, key, assignment))
            {
                continue;
            }
            yield return assignment;
        }
        if (statement is LoopStatement loop)
        {
            // What a counted loop writes in its header is a write of the variable
            // too, and one more at every other trip.
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

    // Whether reading this value where the variable is read computes nothing new and
    // reads nothing a statement in between could have changed. Arithmetic is worth
    // writing twice over; a load is not - a store in between changes what it reads,
    // and a second read of a buffer costs a second load - and an attribute is
    // evaluated where it is read rather than where it was written. A phi is where
    // another path's value arrives.
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
    /// The value the variable was seen to hold - through a chain of variables that
    /// hold variables - and the node itself for anything else, including a variable
    /// that cannot be seen through.
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
    /// The assignments no statement reaches any more, taken out - which is how a
    /// fold that saw through a variable pays for itself. The fold leaves the value it
    /// read dangling: read by the nodes it replaced and by nothing that gets written,
    /// so the variable's reader list still says it is alive when nothing does. Asked
    /// the other way round - is it read from anything that is written - it is not,
    /// and the assignment goes, and with it a statement whose only output it was.
    /// </summary>
    public void RemoveUnreadAssignments(IList<IStatement> statements)
    {
        // One sweep finds only the variables that a still-written assignment read
        // for the last time; a variable another dead assignment was its reader for
        // turns unread on the sweep after it.
        bool removed;
        do
        {
            removed = RemoveUnreadAssignments(statements, statements);
        }
        while (removed);
    }

    // The sweep walks one statement list and removes from it; the liveness walk
    // below always needs the whole function, so the top of it travels along.
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
                // A statement that has stopped assigning anything has stopped being
                // a statement. Only the assignment kind: a store, an append or an
                // interlocked was never assigning its value to a register, and goes
                // on writing it whatever its outputs hold.
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
        // Only a variable's own assignment, never the copy of it a block carries
        // through untouched.
        return value is TempAssignmentNode assignment
            && !IsCarried(statement, key, value)
            && ReferenceEquals(_assignments.GetValueOrDefault(assignment.TempVariable), assignment);
    }

    // Whether the variable is read from anything the function writes. The walk
    // starts from every value every statement writes and reaches back through the
    // inputs to everything on the way down from them; the assignment under test is
    // not one of the starts, because its own value is reachable from its own
    // definition and nothing else proves anyone reads it. A shared value node - the
    // one vector instruction whose components became the components of one variable
    // - says otherwise as soon as anything else reaches it, for that reaching
    // started at a sibling component's assignment, which is still being written.
    // The variable counts as read when a statement writes it as a whole too: the
    // entry holding it is bookkeeping rather than a graph edge, and a reader list
    // that only hangs on edges does not see it.
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
            // Not the expression walk's TraversableInputs: a phi is a leaf to an
            // expression, but a value it carries over a backedge is still read by
            // the loop, and keeping it alive costs this pass nothing it had to
            // spend.
            foreach (HlslTreeNode input in node.Inputs)
            {
                stack.Push(input);
            }
        }
        return written.Contains(assignment.TempVariable)
            || written.Contains(assignment.Value)
            || assignment.TempVariable.Outputs.Any(reader => written.Contains(reader))
            // Components are named together or not at all: where one of them is
            // carried by a store, an append or an interlocked, every component of
            // that instruction stays, and an unread one of such a set would reshape
            // the group the read ones print as.
            || SameInstructionSharedWithHoldingStatement(assignment, statements);
    }

    // The finalizer asks whether a value is carried by a statement that holds it
    // outside the graph, and keeps every component of the same instruction when it
    // is. It asked that before the lowering named the values; this asks it again of
    // the values and the variables they became, and of every sibling component as
    // well as the one under test.
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

    // Whether the statement was handed this register already holding this value and
    // changes nothing - the same test the writer uses to skip a carried value, and
    // the same one that keeps it out of the count of writes.
    private static bool IsCarried(IStatement statement, RegisterComponentKey key, HlslTreeNode value)
    {
        return statement.Inputs.TryGetValue(key, out HlslTreeNode carried)
            && ReferenceEquals(carried, value);
    }

    /// <summary>
    /// The values a statement renders: the entries of its output map that it
    /// writes here, and everything it holds outside the maps.
    /// </summary>
    private static IEnumerable<HlslTreeNode> Roots(IStatement statement)
    {
        switch (statement)
        {
            case AssignmentStatement assignment:
                foreach ((RegisterComponentKey key, HlslTreeNode value) in assignment.Outputs)
                {
                    // A value handed through unchanged is not written here; the
                    // statement that computed it roots it, or the return writes it
                    // from an entry of its own.
                    if (IsCarried(assignment, key, value))
                    {
                        continue;
                    }
                    yield return value;
                }
                break;
            case ReturnStatement returnValue:
                // Every entry, carried included: a returned value arrives at the
                // return carried from where it was computed, and is still what the
                // return prints. Carried or not, an assignment's entry holds that
                // assignment and not its variable, so this does not reach back to
                // hold a dead one alive.
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
