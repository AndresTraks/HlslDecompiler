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

        // All of the length's components, not a subset: normalize(v).xy is not
        // normalize(v.xy). Matched as a set, since order does not affect a length.
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
