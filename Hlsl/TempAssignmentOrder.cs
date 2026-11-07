using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Orders temp assignments so each comes before the readers of its variable. A
/// topological sort, not a comparison sort: the order is partial, and treating
/// "unrelated" as "equal" breaks transitivity. Unrelated items keep their order.
/// </summary>
public class TempAssignmentOrder
{
    /// <param name="wantsNewValueOf">For a non-assignment item, the assignments whose
    /// new value it reads.</param>
    public static List<T> Sort<T>(
        IEnumerable<T> items,
        System.Func<T, HlslTreeNode[]> nodes,
        System.Func<T, IEnumerable<TempAssignmentNode>> wantsNewValueOf = null)
    {
        var entries = items
            .Select(item => new Entry<T>(
                item,
                nodes(item),
                wantsNewValueOf == null ? [] : [.. wantsNewValueOf(item)]))
            .ToList();
        var ordering = new Ordering<T>(entries);
        // Each pair is compared once. By index, not ReferenceEquals: T can be a value
        // type, and an item can come before itself.
        int count = entries.Count;
        var blockers = new int[count];
        var blocks = new List<int>[count];
        for (int j = 0; j < count; j++)
        {
            blocks[j] = [];
            for (int i = 0; i < count; i++)
            {
                if (j != i && ordering.ComesBefore(entries[j], entries[i]))
                {
                    blockers[i]++;
                    blocks[j].Add(i);
                }
            }
        }
        var done = new bool[count];
        var sorted = new List<T>(count);
        for (int step = 0; step < count; step++)
        {
            // The first item nothing left comes before; in a cycle, the first left
            // rather than spin.
            int next = -1;
            int first = -1;
            for (int i = 0; i < count; i++)
            {
                if (done[i])
                {
                    continue;
                }
                if (first < 0)
                {
                    first = i;
                }
                if (blockers[i] == 0)
                {
                    next = i;
                    break;
                }
            }
            if (next < 0)
            {
                next = first;
            }
            done[next] = true;
            sorted.Add(entries[next].Item);
            foreach (int i in blocks[next])
            {
                blockers[i]--;
            }
        }
        return sorted;
    }

    private sealed record Entry<T>(
        T Item, HlslTreeNode[] Nodes, TempAssignmentNode[] Wants)
    {
        public TempAssignmentNode[] Assignments { get; } = [.. Nodes.OfType<TempAssignmentNode>()];

        // The assignments whose new value it reads: as told, or recorded at lowering.
        public HashSet<HlslTreeNode> WantsNew { get; } = new(
            Wants.Concat(Nodes.OfType<TempAssignmentNode>().SelectMany(r => r.DependsOnNewValueOf)),
            ReferenceEqualityComparer.Instance);
    }

    /// <summary>
    /// Pairwise order. Each node records only the sought nodes (the entries'
    /// assignments and variables) it reaches, keeping the sets small, and is walked
    /// once however many entries read it.
    /// </summary>
    private sealed class Ordering<T>
    {
        private readonly HashSet<HlslTreeNode> _sought = HlslTreeNode.NewNodeSet();
        // Per node walked; a node reaching what one input reaches shares its set,
        // so a set is never changed once recorded.
        private readonly Dictionary<HlslTreeNode, HashSet<HlslTreeNode>> _reached =
            new(ReferenceEqualityComparer.Instance);
        private static readonly HashSet<HlslTreeNode> None = HlslTreeNode.NewNodeSet();

        public Ordering(List<Entry<T>> entries)
        {
            foreach (Entry<T> entry in entries)
            {
                foreach (TempAssignmentNode assignment in entry.Assignments)
                {
                    _sought.Add(assignment);
                    _sought.Add(assignment.TempVariable);
                }
            }
        }

        public bool ComesBefore(Entry<T> x, Entry<T> y)
        {
            // Recorded at lowering, the only point where new and old value differ.
            if (NeedsNewValue(y, x))
            {
                return true;
            }
            if (NeedsNewValue(x, y))
            {
                return false;
            }
            // Otherwise a read of a variable the other overwrites wants the old value
            // and goes first; this keeps `t1 = t1 + 1` at the end of a loop body.
            if (ReadsOverwritten(x, y))
            {
                return true;
            }
            if (ReadsOverwritten(y, x))
            {
                return false;
            }
            // Loops, not LINQ: a statement can sort a few hundred items, every pair.
            foreach (TempAssignmentNode assignment in x.Assignments)
            {
                foreach (HlslTreeNode node in y.Nodes)
                {
                    if (Reads(node, assignment) || Reads(node, assignment.TempVariable))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool ReadsOverwritten(Entry<T> readers, Entry<T> written)
        {
            foreach (TempAssignmentNode assignment in written.Assignments)
            {
                if (!assignment.IsReassignment)
                {
                    continue;
                }
                foreach (HlslTreeNode reader in readers.Nodes)
                {
                    if (!ReferenceEquals(reader, assignment) && Reads(reader, assignment.TempVariable))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        // Memoized HlslTreeNode.IsInputOf.
        private bool Reads(HlslTreeNode node, HlslTreeNode sought)
        {
            return Reached(node).Contains(sought);
        }

        private HashSet<HlslTreeNode> Reached(HlslTreeNode node)
        {
            if (_reached.TryGetValue(node, out HashSet<HlslTreeNode> reached))
            {
                return reached;
            }
            reached = None;
            bool owned = false;
            if (_sought.Contains(node))
            {
                reached = HlslTreeNode.NewNodeSet();
                reached.Add(node);
                owned = true;
            }
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                HashSet<HlslTreeNode> inner = Reached(input);
                if (inner.Count == 0 || ReferenceEquals(inner, reached))
                {
                    continue;
                }
                if (reached.Count == 0)
                {
                    reached = inner;
                    continue;
                }
                if (!owned)
                {
                    reached = new HashSet<HlslTreeNode>(reached, ReferenceEqualityComparer.Instance);
                    owned = true;
                }
                reached.UnionWith(inner);
            }
            _reached[node] = reached;
            return reached;
        }
    }

    public static List<HlslTreeNode[]> Sort(IEnumerable<HlslTreeNode[]> groups)
    {
        return Sort(groups, g => g);
    }

    public static List<T> SortNodes<T>(IEnumerable<T> items) where T : HlslTreeNode
    {
        return Sort(items, n => new HlslTreeNode[] { n });
    }

    /// <summary>Whether <paramref name="reader"/> wants the new value of something
    /// <paramref name="written"/> assigns.</summary>
    private static bool NeedsNewValue<T>(Entry<T> reader, Entry<T> written)
    {
        foreach (TempAssignmentNode assignment in written.Assignments)
        {
            if (reader.WantsNew.Contains(assignment))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="later"/> wants the old value of a variable that
    /// <paramref name="earlier"/> overwrites, so this order reads the wrong value.
    /// </summary>
    public static bool ReadsStale(HlslTreeNode[] later, HlslTreeNode[] earlier)
    {
        IEnumerable<TempAssignmentNode> wants = later.OfType<TempAssignmentNode>()
            .SelectMany(r => r.DependsOnNewValueOf);
        if (earlier.OfType<TempAssignmentNode>().Any(assignment =>
            wants.Any(fed => ReferenceEquals(fed, assignment))))
        {
            return false;
        }
        return ReadsOverwritten(later, earlier);
    }

    private static bool ReadsOverwritten(HlslTreeNode[] readers, HlslTreeNode[] written)
    {
        return written.OfType<TempAssignmentNode>().Any(assignment =>
            assignment.IsReassignment
            && readers.Any(reader => !ReferenceEquals(reader, assignment)
                && assignment.TempVariable.IsInputOf(reader)));
    }

    // A read of the variable counts too: a variable is a leaf, so the assignment
    // node itself is not reachable through it.
    private static bool IsInputOf(HlslTreeNode input, HlslTreeNode node)
    {
        if (input is TempAssignmentNode tempAssignment)
        {
            if (tempAssignment.IsInputOf(node) || tempAssignment.TempVariable.IsInputOf(node))
            {
                return true;
            }
        }
        return false;
    }
}
