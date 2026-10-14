using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// Backward liveness of variables after each statement over the statement tree.
/// Errs towards live, since a false "dead" could merge two wanted values: a
/// statement's own reads count as live after it, and a switch is not modelled
/// (everything inside answers unknown).
/// </summary>
internal sealed class VariableLiveness
{
    private readonly Dictionary<IStatement, HashSet<TempVariableNode>> _liveAfter =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<IStatement> _unknown = new(ReferenceEqualityComparer.Instance);

    private readonly bool _exact;

    private VariableLiveness(bool exact)
    {
        _exact = exact;
    }

    /// <param name="exact">
    /// A read of a variable the same statement assigns first is of the new value,
    /// not live-in. For carried-value questions, not for merging variables.
    /// </param>
    public static VariableLiveness Analyze(IList<IStatement> statements, bool exact = false)
    {
        var liveness = new VariableLiveness(exact);
        liveness.Body(statements, [], [], []);
        return liveness;
    }

    /// <summary>
    /// Live after the statement, counting its own reads; true if unmodelled.
    /// </summary>
    public bool MayBeLiveAfter(TempVariableNode variable, IStatement statement)
    {
        return _unknown.Contains(statement)
            || !_liveAfter.TryGetValue(statement, out HashSet<TempVariableNode> live)
            || live.Contains(variable)
            || Reads(statement).Contains(variable);
    }

    /// <summary>
    /// Like <see cref="MayBeLiveAfter"/> without the statement's own reads.
    /// </summary>
    public bool MayBeReadAfter(TempVariableNode variable, IStatement statement)
    {
        return _unknown.Contains(statement)
            || !_liveAfter.TryGetValue(statement, out HashSet<TempVariableNode> live)
            || live.Contains(variable);
    }

    private HashSet<TempVariableNode> Body(IList<IStatement> body, HashSet<TempVariableNode> liveOut,
        HashSet<TempVariableNode> breakLive, HashSet<TempVariableNode> continueLive)
    {
        HashSet<TempVariableNode> live = liveOut;
        for (int i = body.Count - 1; i >= 0; i--)
        {
            IStatement statement = body[i];
            if (!_liveAfter.TryGetValue(statement, out HashSet<TempVariableNode> after))
            {
                after = NewSet();
                _liveAfter[statement] = after;
            }
            after.UnionWith(live);
            live = Transfer(statement, live, breakLive, continueLive);
        }
        return live;
    }

    private HashSet<TempVariableNode> Transfer(IStatement statement, HashSet<TempVariableNode> liveOut,
        HashSet<TempVariableNode> breakLive, HashSet<TempVariableNode> continueLive)
    {
        switch (statement)
        {
            case IfStatement ifStatement:
                {
                    HashSet<TempVariableNode> live = Body(ifStatement.TrueBody, liveOut, breakLive, continueLive);
                    live.UnionWith(ifStatement.FalseBody == null
                        ? liveOut
                        : Body(ifStatement.FalseBody, liveOut, breakLive, continueLive));
                    live.UnionWith(Reads(statement));
                    return live;
                }
            case LoopStatement loop:
                {
                    HashSet<TempVariableNode> head = NewSet();
                    while (true)
                    {
                        // The end of a pass: the increment, then the head again.
                        HashSet<TempVariableNode> endOfPass = NewSet(head);
                        if (loop.Increment != null)
                        {
                            endOfPass.ExceptWith(Variables([loop.Increment.TempVariable]));
                            endOfPass.UnionWith(Variables([loop.Increment.Value]));
                        }
                        HashSet<TempVariableNode> start = Body(loop.Body, endOfPass, liveOut, endOfPass);
                        // The head test may exit; adding liveOut regardless errs live.
                        start.UnionWith(liveOut);
                        start.UnionWith(Variables([loop.ContinueCondition, loop.RepeatCountNode]));
                        if (start.IsSubsetOf(head))
                        {
                            break;
                        }
                        head.UnionWith(start);
                    }
                    HashSet<TempVariableNode> live = NewSet(head);
                    if (loop.Initializer != null)
                    {
                        live.ExceptWith(Variables([loop.Initializer.TempVariable]));
                        live.UnionWith(Variables([loop.Initializer.Value]));
                    }
                    return live;
                }
            case SwitchStatement switchStatement:
                {
                    // Not modelled: kills nothing, needs everything read inside.
                    HashSet<TempVariableNode> live = NewSet(liveOut);
                    new StatementVisitor([switchStatement]).Visit(inner =>
                    {
                        _unknown.Add(inner);
                        live.UnionWith(Reads(inner));
                    });
                    return live;
                }
            case BreakStatement breakStatement:
                return Jump(breakStatement.Comparison != null, liveOut, breakLive, statement);
            case ContinueStatement continueStatement:
                return Jump(continueStatement.Comparison != null, liveOut, continueLive, statement);
            case ReturnStatement returnStatement:
                return Jump(returnStatement.Comparison != null, liveOut, [], statement);
            case DiscardStatement discard:
                return Jump(discard.Comparison != null, liveOut, [], statement);
            default:
                {
                    HashSet<TempVariableNode> live = NewSet(liveOut);
                    live.ExceptWith(Writes(statement));
                    live.UnionWith(_exact ? ReadsBeforeWrites(statement) : Reads(statement));
                    return live;
                }
        }
    }

    // A jump goes where it goes; a conditional one also falls through.
    private static HashSet<TempVariableNode> Jump(bool conditional, HashSet<TempVariableNode> liveOut,
        HashSet<TempVariableNode> target, IStatement statement)
    {
        HashSet<TempVariableNode> live = NewSet(target);
        if (conditional)
        {
            live.UnionWith(liveOut);
        }
        live.UnionWith(Reads(statement));
        return live;
    }

    /// <summary>
    /// Performed (not carried) assignments and an atomic's old-value variable.
    /// Compound statements assign nothing themselves.
    /// </summary>
    public static HashSet<TempVariableNode> Writes(IStatement statement)
    {
        HashSet<TempVariableNode> writes = NewSet();
        if (statement is IfStatement or LoopStatement or SwitchStatement)
        {
            return writes;
        }
        foreach (TempAssignmentNode assignment in Performed(statement).OfType<TempAssignmentNode>())
        {
            writes.Add(assignment.TempVariable);
        }
        if (statement is AtomicStatement { Original: TempVariableNode original })
        {
            writes.Add(original);
        }
        return writes;
    }

    /// <summary>
    /// Variables read by what it assigns and by everything it holds.
    /// </summary>
    public static HashSet<TempVariableNode> Reads(IStatement statement)
    {
        IEnumerable<HlslTreeNode> roots = statement.HeldNodes;
        if (statement is not (IfStatement or LoopStatement or SwitchStatement))
        {
            roots = roots.Concat(Performed(statement)
                .Select(value => value is TempAssignmentNode assignment ? assignment.Value : value));
        }
        return Variables(roots);
    }

    // Reads of earlier values only, not through its own assignments.
    private static HashSet<TempVariableNode> ReadsBeforeWrites(IStatement statement)
    {
        HashSet<HlslTreeNode> own = HlslTreeNode.NewNodeSet();
        foreach (TempAssignmentNode assignment in Performed(statement).OfType<TempAssignmentNode>())
        {
            own.Add(assignment);
        }
        HashSet<TempVariableNode> reads = Variables(statement.HeldNodes, own);
        foreach (HlslTreeNode value in Performed(statement))
        {
            if (value is not TempAssignmentNode assignment)
            {
                reads.UnionWith(Variables([value], own));
                continue;
            }
            // A wanted new value from this statement is not a read of the old one.
            HashSet<TempVariableNode> read = Variables([assignment.Value], own);
            foreach (TempAssignmentNode earlier in assignment.DependsOnNewValueOf)
            {
                if (own.Contains(earlier))
                {
                    read.Remove(earlier.TempVariable);
                }
            }
            reads.UnionWith(read);
        }
        return reads;
    }

    // The outputs a statement computes rather than carries.
    private static IEnumerable<HlslTreeNode> Performed(IStatement statement)
    {
        return statement.Outputs
            .Where(output => !statement.Inputs.TryGetValue(output.Key, out HlslTreeNode input)
                || !ReferenceEquals(input, output.Value))
            .Select(output => output.Value);
    }

    // An assignment node read counts as a read of its variable.
    private static HashSet<TempVariableNode> Variables(IEnumerable<HlslTreeNode> roots,
        HashSet<HlslTreeNode> own = null)
    {
        HashSet<TempVariableNode> variables = NewSet();
        HashSet<HlslTreeNode> seen = HlslTreeNode.NewNodeSet();
        var pending = new Stack<HlslTreeNode>(roots.Where(root => root != null));
        while (pending.Count != 0)
        {
            HlslTreeNode node = pending.Pop();
            if (node == null || !seen.Add(node))
            {
                continue;
            }
            switch (node)
            {
                case TempVariableNode variable:
                    variables.Add(variable);
                    continue;
                case TempAssignmentNode assignment:
                    if (own == null || !own.Contains(assignment))
                    {
                        variables.Add(assignment.TempVariable);
                    }
                    continue;
            }
            foreach (HlslTreeNode input in node.Inputs)
            {
                pending.Push(input);
            }
        }
        return variables;
    }

    private static HashSet<TempVariableNode> NewSet(IEnumerable<TempVariableNode> items = null)
    {
        var set = new HashSet<TempVariableNode>(ReferenceEqualityComparer.Instance);
        if (items != null)
        {
            set.UnionWith(items);
        }
        return set;
    }
}
