using System.Collections.Generic;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Three components that are a cross product: `a.yzx * b.zxy - a.zxy * b.yzx`,
/// which is how fxc writes cross(a, b) - a mul and a mad with a negated addend,
/// and after the templates three subtracts of two products each.
/// </summary>
public class CrossProductGrouper
{
    /// <summary>
    /// The two vectors crossed, as components, or null.
    /// </summary>
    public (HlslTreeNode[] A, HlslTreeNode[] B)? TryGetContext(IList<HlslTreeNode> components)
    {
        if (components.Count != 3)
        {
            return null;
        }
        var products = new (HlslTreeNode[] First, HlslTreeNode[] Second)[3];
        for (int i = 0; i < 3; i++)
        {
            if (components[i] is not SubtractOperation subtract
                || subtract.Minuend is not MultiplyOperation first
                || subtract.Subtrahend is not MultiplyOperation second)
            {
                return null;
            }
            products[i] = ([first.Factor1, first.Factor2], [second.Factor1, second.Factor2]);
        }

        // Component i is a[j] * b[k] - a[k] * b[j], j and k the components after it.
        // Which factor of the first two products is a's decides the rest: b[0] is
        // what a[2] is multiplied by in component 1, and a[0] what b[2] is, and the
        // last component is checked against all of it.
        foreach (int firstA in new[] { 0, 1 })
        {
            foreach (int secondA in new[] { 0, 1 })
            {
                var a = new HlslTreeNode[3];
                var b = new HlslTreeNode[3];
                a[1] = products[0].First[firstA];
                b[2] = products[0].First[1 - firstA];
                a[2] = products[0].Second[secondA];
                b[1] = products[0].Second[1 - secondA];
                b[0] = OtherFactor(products[1].First, a[2]);
                a[0] = OtherFactor(products[1].Second, b[2]);
                if (b[0] == null || a[0] == null)
                {
                    continue;
                }
                if (IsProduct(products[2].First, a[0], b[1]) && IsProduct(products[2].Second, a[1], b[0]))
                {
                    return (a, b);
                }
            }
        }
        return null;
    }

    // The factor a product multiplies the given node by, or null where the node is
    // not one of its factors.
    private static HlslTreeNode OtherFactor(HlslTreeNode[] factors, HlslTreeNode node)
    {
        if (NodeGrouper.AreNodesEquivalent(factors[0], node))
        {
            return factors[1];
        }
        if (NodeGrouper.AreNodesEquivalent(factors[1], node))
        {
            return factors[0];
        }
        return null;
    }

    private static bool IsProduct(HlslTreeNode[] factors, HlslTreeNode x, HlslTreeNode y)
    {
        return (NodeGrouper.AreNodesEquivalent(factors[0], x) && NodeGrouper.AreNodesEquivalent(factors[1], y))
            || (NodeGrouper.AreNodesEquivalent(factors[0], y) && NodeGrouper.AreNodesEquivalent(factors[1], x));
    }
}
