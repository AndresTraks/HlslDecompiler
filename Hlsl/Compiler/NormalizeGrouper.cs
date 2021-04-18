using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.Hlsl
{
    public class NormalizeGrouper
    {
        public HlslTreeNode[] TryGetContext(IList<HlslTreeNode> components)
        {
            var firstComponent = components[0];
            if (!(firstComponent is DivisionOperation firstDivision) ||
                !(firstDivision.Divisor is LengthOperation firstLength))
            {
                return null;
            }

            if (!firstLength.X.Inputs.Any(c => NodeGrouper.AreNodesEquivalent(firstDivision.Dividend, c)))
            {
                return null;
            }

            int normalizeComponentCount = 1;
            for (int i = 1; i < components.Count; i++)
            {
                if (IsNormalizeGroupComponent(components[i], firstDivision, firstLength))
                {
                    normalizeComponentCount++;
                }
                else
                {
                    break;
                }
            }

            if (normalizeComponentCount < 2)
            {
                return null;
            }

            return components
                .Take(normalizeComponentCount)
                .Cast<DivisionOperation>()
                .Select(c => c.Dividend)
                .ToArray();
        }

        private static bool IsNormalizeGroupComponent(HlslTreeNode nextComponent, DivisionOperation firstDivision, LengthOperation firstLength)
        {
            return nextComponent is DivisionOperation nextDivision
                && NodeGrouper.AreNodesEquivalent(nextDivision.Divisor, firstDivision.Divisor)
                && firstLength.X.Inputs.Any(c => NodeGrouper.AreNodesEquivalent(nextDivision.Dividend, c));
        }
    }
}
