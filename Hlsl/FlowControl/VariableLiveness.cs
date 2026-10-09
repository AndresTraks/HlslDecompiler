using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl.FlowControl;

/// <summary>
/// Which variables are live after each statement: read on some path from there
/// before being assigned again. Worked out backwards over the statement tree -
/// an if joins what its branches need, a loop is iterated until what its head
/// needs stops growing, a break and a continue need what follows the loop and
/// what starts its next pass.
///
/// Every answer errs towards live. What asks this is whether two variables can
/// be one, and a variable taken for live only keeps the two apart, where one
/// taken for dead could merge two values that are both still wanted. So a
/// statement's own reads count as live after it even where it writes the
/// variable first, and a switch - whose breaks leave the switch rather than a
/// loop - is not modelled at all: whatever is inside one answers "unknown".
/// </summary>
internal sealed class VariableLiveness
{
    private readonly Dictionary<IStatement, HashSet<TempVariableNode>> _liveAfter =
        new(ReferenceEqualityComparer.Instance);
    private readonly HashSet<IStatement> _unknown = new(ReferenceEqualityComparer.Instance);

    // Whether a statement reading a value it assigns itself reads the variable.
    // By default it does, which errs towards live; exact, it reads the new value
    // and not the variable's old one.
    private readonly bool _exact;

    private VariableLiveness(bool exact)
    {
        _exact = exact;
    }

    /// <param name="exact">
    /// Leave out of a statement's reads the assignments it performs itself: `t1 =
    /// g0[i]; t6 = g0[t6] + t1;` as one statement reads the t1 it has just written,
    /// not what t1 held before. For asking whether a value is carried in from
    /// before - not for merging two variables, which wants the default.
    /// </param>
    public static VariableLiveness Analyze(IList<IStatement> statements, bool exact = false)
    {
        var liveness = new VariableLiveness(exact);
        liveness.Body(statements, [], [], []);
        return liveness;
    }

    /// <summary>
    /// Whether the variable may be read after the statement before it is assigned
    /// again - or the statement is one this does not model, which answers yes.
    /// </summary>
    public bool MayBeLiveAfter(TempVariableNode variable, IStatement statement)
    {
        return _unknown.Contains(statement)
            || !_liveAfter.TryGetValue(statement, out HashSet<TempVariableNode> live)
            || live.Contains(variable)
            || Reads(statement).Contains(variable);
    }

    /// <summary>
    /// Whether the variable may be read by a statement after this one, leaving out
    /// the statement's own reads - which is what is live on entry to the statement
    /// that follows it.
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
                        // The test at the head may leave the loop, and a loop with no
                        // test is left by its breaks, which Body has already joined -
                        // what follows the loop is added either way, which only errs
                        // towards live.
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
                    // Not modelled: everything inside answers unknown, and the switch
                    // itself kills nothing and needs everything it reads.
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
    /// The variables a statement assigns: the assignments it performs - an output
    /// that is not the value the register came in with - and the old value an
    /// interlocked operation writes back. A compound statement assigns nothing of
    /// its own; its bodies do.
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
    /// The variables a statement reads: in what it assigns, in the registers it
    /// writes, and in everything it holds - a condition, a stored value, a clause.
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

    // The variables a statement reads the earlier value of: its reads, but not
    // through an assignment it performs itself.
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
            // Read by name, a variable is the new value where the assignment says
            // it wants that - an assignment this statement performs first.
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

    // The variables an expression reads: the variables in it, and the variable of
    // an assignment it reads the value of.
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
