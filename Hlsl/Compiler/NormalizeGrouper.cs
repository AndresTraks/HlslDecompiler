using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl;

public class NormalizeGrouper
{
    public HlslTreeNode[] TryGetContext(IList<HlslTreeNode> components)
    {
        var firstComponent = components[0];
        if (firstComponent is not DivisionOperation firstDivision ||
            firstDivision.Divisor is not LengthOperation firstLength)
        {
            return null;
        }

        int normalizeComponentCount = 1;
        for (int i = 1; i < components.Count; i++)
        {
            if (IsNormalizeGroupComponent(components[i], firstDivision))
            {
                normalizeComponentCount++;
            }
            else
            {
                break;
            }
        }

        // The whole of what the length was taken over, and not part of it. Part is
        // not a normalize of that part: a parallax shader normalized a tangent
        // space view vector and then used its .xy and its .z apart, and the two
        // components on their own came out as normalize(v.xy), which is a
        // different vector. Where the group is not the whole, the divisions stand
        // as they are and say the same thing.
        //
        // Matched as a set rather than in order, since a length does not care:
        // normalize(position.yxz) divides by the length of x, y and z.
        HlslTreeNode[] dividends = [.. components
            .Take(normalizeComponentCount)
            .Cast<DivisionOperation>()
            .Select(c => c.Dividend)];
        var unmatched = new List<HlslTreeNode>(firstLength.X.Inputs);
        if (dividends.Length != unmatched.Count)
        {
            return null;
        }
        foreach (HlslTreeNode dividend in dividends)
        {
            int index = unmatched.FindIndex(c => NodeGrouper.AreNodesEquivalent(dividend, c));
            if (index < 0)
            {
                return null;
            }
            unmatched.RemoveAt(index);
        }
        return dividends;
    }

    private static bool IsNormalizeGroupComponent(HlslTreeNode nextComponent, DivisionOperation firstDivision)
    {
        return nextComponent is DivisionOperation nextDivision
            && NodeGrouper.AreNodesEquivalent(nextDivision.Divisor, firstDivision.Divisor);
    }
}
