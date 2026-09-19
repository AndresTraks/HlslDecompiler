using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

/// <summary>
/// Components that are a reflection. HLSL defines `reflect(i, n)` as
/// `i - 2 * dot(i, n) * n`, and that is what fxc writes: a dot, an add of the
/// result to itself, and a mad per component with the scale negated. Written out
/// it is two statements of vector algebra that the reader has to recognise.
///
/// The incident and the normal are taken from the dot rather than from the
/// components, because the incident may be a negation the components express by
/// subtracting: `n * -s - l` is `reflect(-l, n)`, and the `-l` the dot was taken
/// over is the node to hand back.
/// </summary>
public class ReflectGrouper
{
    public (HlslTreeNode[] Incident, HlslTreeNode[] Normal)? TryGetContext(IList<HlslTreeNode> components)
    {
        if (components.Count < 2)
        {
            return null;
        }
        var normals = new HlslTreeNode[components.Count];
        var addends = new HlslTreeNode[components.Count];
        var subtracted = new bool[components.Count];
        HlslTreeNode scale = null;
        for (int i = 0; i < components.Count; i++)
        {
            if (!TryDecompose(components[i], ref scale, out normals[i], out addends[i], out subtracted[i]))
            {
                return null;
            }
        }

        if (TryGetDotProduct(scale) is not DotProductOperation dot)
        {
            return null;
        }
        GroupNode[] sides = [dot.X, dot.Y];
        foreach (GroupNode normalSide in sides)
        {
            GroupNode incidentSide = ReferenceEquals(normalSide, dot.X) ? dot.Y : dot.X;
            if (normalSide.Inputs.Count != components.Count
                || !Matches(normalSide, normals, negated: false)
                || !Matches(incidentSide, addends, negated: subtracted))
            {
                continue;
            }
            return ([.. incidentSide.Inputs], [.. normalSide.Inputs]);
        }
        return null;
    }

    // incident + normal * -scale, however the component spells it.
    private static bool TryDecompose(
        HlslTreeNode component, ref HlslTreeNode scale,
        out HlslTreeNode normal, out HlslTreeNode addend, out bool subtracted)
    {
        normal = null;
        addend = null;
        subtracted = false;
        (HlslTreeNode Left, HlslTreeNode Right, bool Subtracted)[] splits = component switch
        {
            AddOperation add => [(add.Addend1, add.Addend2, false), (add.Addend2, add.Addend1, false)],
            SubtractOperation subtract => [(subtract.Minuend, subtract.Subtrahend, true)],
            _ => [],
        };
        foreach ((HlslTreeNode left, HlslTreeNode right, bool isSubtract) in splits)
        {
            if (left is not MultiplyOperation multiply)
            {
                continue;
            }
            HlslTreeNode scaled = multiply.Factor1 is NegateOperation ? multiply.Factor1 : multiply.Factor2;
            HlslTreeNode other = ReferenceEquals(scaled, multiply.Factor1) ? multiply.Factor2 : multiply.Factor1;
            if (scaled is not NegateOperation negate
                || (scale != null && !ReferenceEquals(scale, negate.Value)))
            {
                continue;
            }
            scale = negate.Value;
            normal = other;
            addend = right;
            subtracted = isSubtract;
            return true;
        }
        return false;
    }

    // The scale is the dot taken twice - `2 * dot(i, n)`, or the add of it to
    // itself that fxc actually emits, before AddSelfTemplate has been at it.
    private static DotProductOperation TryGetDotProduct(HlslTreeNode scale)
    {
        if (scale is AddOperation add
            && ReferenceEquals(add.Addend1, add.Addend2))
        {
            return add.Addend1 as DotProductOperation;
        }
        if (scale is not MultiplyOperation multiply)
        {
            return null;
        }
        if (IsTwo(multiply.Factor1))
        {
            return multiply.Factor2 as DotProductOperation;
        }
        return IsTwo(multiply.Factor2) ? multiply.Factor1 as DotProductOperation : null;
    }

    private static bool IsTwo(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value == 2f;
    }

    private static bool Matches(GroupNode side, HlslTreeNode[] nodes, bool negated)
    {
        return Matches(side, nodes, Enumerable.Repeat(negated, nodes.Length).ToArray());
    }

    // A side of the dot, against what the components read. Where the component
    // subtracted its addend, the dot's own input is the negation of it.
    private static bool Matches(GroupNode side, HlslTreeNode[] nodes, bool[] negated)
    {
        if (side.Inputs.Count != nodes.Length)
        {
            return false;
        }
        for (int i = 0; i < nodes.Length; i++)
        {
            HlslTreeNode input = side.Inputs[i];
            bool matched = negated[i]
                ? input is NegateOperation negate && NodeGrouper.AreNodesEquivalent(negate.Value, nodes[i])
                : NodeGrouper.AreNodesEquivalent(input, nodes[i]);
            if (!matched)
            {
                return false;
            }
        }
        return true;
    }
}
