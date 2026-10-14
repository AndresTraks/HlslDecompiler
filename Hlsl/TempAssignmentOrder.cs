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
        var remaining = items
            .Select((item, index) => new Entry<T>(
                item,
                nodes(item),
                wantsNewValueOf == null ? [] : [.. wantsNewValueOf(item)],
                index))
            .ToList();
        // Pairs are compared repeatedly and the graph does not change, so memoize.
        var ordering = new Ordering<T>(remaining);
        var sorted = new List<T>(remaining.Count);
        while (remaining.Count != 0)
        {
            // By index, not ReferenceEquals: T can be a value type, and an item can
            // come before itself.
            int next = -1;
            for (int i = 0; i < remaining.Count; i++)
            {
                bool blocked = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    if (j != i && ordering.ComesBefore(remaining[j], remaining[i]))
                    {
                        blocked = true;
                        break;
                    }
                }
                if (!blocked)
                {
                    next = i;
                    break;
                }
            }
            // A cycle; take the first rather than spin.
            if (next < 0)
            {
                next = 0;
            }
            sorted.Add(remaining[next].Item);
            remaining.RemoveAt(next);
        }
        return sorted;
    }

    private sealed record Entry<T>(
        T Item, HlslTreeNode[] Nodes, TempAssignmentNode[] Wants, int Index);

    /// <summary>
    /// Memoized pairwise order. Each walk records only the sought nodes (the entries'
    /// assignments and variables) it reaches, keeping the sets small.
    /// </summary>
    private sealed class Ordering<T>
    {
        // Sparse dictionary, not an n-squared table.
        private readonly Dictionary<(int, int), bool> _pairs = [];
        private readonly HashSet<HlslTreeNode> _sought = HlslTreeNode.NewNodeSet();
        private readonly Dictionary<HlslTreeNode, HashSet<HlslTreeNode>> _reached =
            new(ReferenceEqualityComparer.Instance);

        public Ordering(List<Entry<T>> entries)
        {
            foreach (Entry<T> entry in entries)
            {
                foreach (TempAssignmentNode assignment in entry.Nodes.OfType<TempAssignmentNode>())
                {
                    _sought.Add(assignment);
                    _sought.Add(assignment.TempVariable);
                }
            }
        }

        public bool ComesBefore(Entry<T> x, Entry<T> y)
        {
            if (_pairs.TryGetValue((x.Index, y.Index), out bool remembered))
            {
                return remembered;
            }
            bool answer = Compare(x, y);
            _pairs[(x.Index, y.Index)] = answer;
            return answer;
        }

        private bool Compare(Entry<T> x, Entry<T> y)
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
            if (ReadsOverwritten(x.Nodes, y.Nodes))
            {
                return true;
            }
            if (ReadsOverwritten(y.Nodes, x.Nodes))
            {
                return false;
            }
            return x.Nodes.Any(i => y.Nodes.Any(i2 => IsInputOf(i, i2)));
        }

        private bool ReadsOverwritten(HlslTreeNode[] readers, HlslTreeNode[] written)
        {
            return written.OfType<TempAssignmentNode>().Any(assignment =>
                assignment.IsReassignment
                && readers.Any(reader => !ReferenceEquals(reader, assignment)
                    && Reads(reader, assignment.TempVariable)));
        }

        private bool IsInputOf(HlslTreeNode input, HlslTreeNode node)
        {
            return input is TempAssignmentNode tempAssignment
                && (Reads(node, tempAssignment) || Reads(node, tempAssignment.TempVariable));
        }

        // Memoized HlslTreeNode.IsInputOf.
        private bool Reads(HlslTreeNode node, HlslTreeNode sought)
        {
            if (!_reached.TryGetValue(node, out HashSet<HlslTreeNode> reached))
            {
                _reached[node] = reached = HlslTreeNode.NewNodeSet();
                Walk(node, reached, HlslTreeNode.NewNodeSet());
            }
            return reached.Contains(sought);
        }

        private void Walk(HlslTreeNode node, HashSet<HlslTreeNode> reached,
            HashSet<HlslTreeNode> visited)
        {
            if (!visited.Add(node))
            {
                return;
            }
            if (_sought.Contains(node))
            {
                reached.Add(node);
            }
            foreach (HlslTreeNode input in HlslTreeNode.TraversableInputs(node))
            {
                Walk(input, reached, visited);
            }
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
        IEnumerable<TempAssignmentNode> wants = reader.Wants
            .Concat(reader.Nodes.OfType<TempAssignmentNode>().SelectMany(r => r.DependsOnNewValueOf));
        return written.Nodes.OfType<TempAssignmentNode>().Any(assignment =>
            wants.Any(fed => ReferenceEquals(fed, assignment)));
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
