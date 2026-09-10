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
    public static List<T> Sort<T>(IEnumerable<T> items, System.Func<T, HlslTreeNode[]> nodes)
    {
        var remaining = items.ToList();
        var sorted = new List<T>(remaining.Count);
        while (remaining.Count != 0)
        {
            int next = remaining.FindIndex(
                candidate => !remaining.Any(other => !ReferenceEquals(other, candidate)
                    && ComesBefore(nodes(other), nodes(candidate))));
            // Every one of them waits on another, which a value graph cannot really
            // be. Take the first rather than spin.
            if (next < 0)
            {
                next = 0;
            }
            sorted.Add(remaining[next]);
            remaining.RemoveAt(next);
        }
        return sorted;
    }

    public static List<HlslTreeNode[]> Sort(IEnumerable<HlslTreeNode[]> groups)
    {
        return Sort(groups, g => g);
    }

    public static List<T> SortNodes<T>(IEnumerable<T> items) where T : HlslTreeNode
    {
        return Sort(items, n => new HlslTreeNode[] { n });
    }

    private static bool ComesBefore(HlslTreeNode[] x, HlslTreeNode[] y)
    {
        return x.Any(i => y.Any(i2 => IsInputOf(i, i2)));
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
