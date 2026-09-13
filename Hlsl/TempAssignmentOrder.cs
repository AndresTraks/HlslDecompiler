using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Orders temp assignments so that one is written before anything that reads the
/// variable it assigns.
///
/// This is a partial order - most pairs of assignments have nothing to do with each
/// other - and a partial order cannot be handed to a comparison sort. "Unrelated"
/// comes back as "equal", equal is then not transitive, and insertion sort stops at
/// the first element that does not compare greater, which can be one that should
/// have come after. With four assignments and one real dependency between them, the
/// sort put the variable's use before its assignment.
///
/// So it sorts rather than compares: repeatedly take the first assignment whose
/// dependencies have all been placed, which keeps the original order among
/// assignments that do not depend on each other.
/// </summary>
public class TempAssignmentOrder
{
    /// <param name="wantsNewValueOf">For an item that is not itself an assignment -
    /// an output register's value - the assignments whose value it wants as this
    /// statement computes it. An assignment carries that record on itself.</param>
    public static List<T> Sort<T>(
        IEnumerable<T> items,
        System.Func<T, HlslTreeNode[]> nodes,
        System.Func<T, IEnumerable<TempAssignmentNode>> wantsNewValueOf = null)
    {
        var remaining = items
            .Select(item => new Entry<T>(
                item,
                nodes(item),
                wantsNewValueOf == null ? [] : [.. wantsNewValueOf(item)]))
            .ToList();
        var sorted = new List<T>(remaining.Count);
        while (remaining.Count != 0)
        {
            // By index, not ReferenceEquals: T can be a value type (a tuple, a
            // KeyValuePair), and comparing two boxed structs by reference never
            // excludes an item from being checked against itself - which, for a
            // TempAssignmentNode, ComesBefore can answer true to, blocking the one
            // real candidate every round and leaving the fallback below to paper
            // over it by accident.
            int next = -1;
            for (int i = 0; i < remaining.Count; i++)
            {
                bool blocked = false;
                for (int j = 0; j < remaining.Count; j++)
                {
                    if (j != i && ComesBefore(remaining[j], remaining[i]))
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
            // Every one of them waits on another, which a value graph cannot really
            // be. Take the first rather than spin.
            if (next < 0)
            {
                next = 0;
            }
            sorted.Add(remaining[next].Item);
            remaining.RemoveAt(next);
        }
        return sorted;
    }

    private sealed record Entry<T>(T Item, HlslTreeNode[] Nodes, TempAssignmentNode[] Wants);

    public static List<HlslTreeNode[]> Sort(IEnumerable<HlslTreeNode[]> groups)
    {
        return Sort(groups, g => g);
    }

    public static List<T> SortNodes<T>(IEnumerable<T> items) where T : HlslTreeNode
    {
        return Sort(items, n => new HlslTreeNode[] { n });
    }

    private static bool ComesBefore<T>(Entry<T> x, Entry<T> y)
    {
        // What one write wants of another is recorded at lowering, which is the
        // only point at which wanting the value this statement computes and wanting
        // the one the register held before are different things.
        if (NeedsNewValue(y, x))
        {
            return true;
        }
        if (NeedsNewValue(x, y))
        {
            return false;
        }
        // Neither wants the other new, so a read of a variable the other overwrites
        // is a read of the value from before, and has to come first. This is what
        // keeps `t1 = t1 + 1` at the end of a loop body.
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

    /// <summary>Whether <paramref name="reader"/> wants a value that any of
    /// <paramref name="written"/> computes here, rather than its earlier one.</summary>
    private static bool NeedsNewValue<T>(Entry<T> reader, Entry<T> written)
    {
        IEnumerable<TempAssignmentNode> wants = reader.Wants
            .Concat(reader.Nodes.OfType<TempAssignmentNode>().SelectMany(r => r.DependsOnNewValueOf));
        return written.Nodes.OfType<TempAssignmentNode>().Any(assignment =>
            wants.Any(fed => ReferenceEquals(fed, assignment)));
    }

    private static bool ReadsOverwritten(HlslTreeNode[] readers, HlslTreeNode[] written)
    {
        return written.OfType<TempAssignmentNode>().Any(assignment =>
            assignment.IsReassignment
            && readers.Any(reader => !ReferenceEquals(reader, assignment)
                && assignment.TempVariable.IsInputOf(reader)));
    }

    // What makes an assignment a dependency is that the variable it assigns is read,
    // not that the assignment node itself is reachable - a variable is a leaf, so it
    // never is.
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
