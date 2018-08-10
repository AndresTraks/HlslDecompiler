using System.Collections.Generic;

namespace HlslDecompiler.Hlsl
{
    public class NormalizeGrouper
    {
        private readonly NodeGrouper _nodeGrouper;

        public NormalizeGrouper(NodeGrouper nodeGrouper)
        {
            _nodeGrouper = nodeGrouper;
        }

        public HlslTreeNode[] TryGetContext(IList<HlslTreeNode> components)
        {
            var firstComponent = components[0];
            if (!(firstComponent is DivisionOperation firstDivision))
            {
                return null;
            }

            var firstLengthContext = _nodeGrouper.LengthGrouper.TryGetLengthContext(firstDivision.Divisor);
            if (firstLengthContext == null)
            {
                return null;
            }

            int dimension = firstLengthContext.Length;

            if (NodeGrouper.AreNodesEquivalent(firstDivision.Dividend, firstLengthContext[0]) == false)
            {
                return null;
            }

            for (int i = 1; i < dimension; i++)
            {
                if (i >= components.Count)
                {
                    return null;
                }

                var nextComponent = components[i];
                if (!(nextComponent is DivisionOperation nextDivision))
                {
                    return null;
                }

                if (NodeGrouper.AreNodesEquivalent(nextDivision.Divisor, firstDivision.Divisor) == false)
                {
                    return null;
                }

                if (NodeGrouper.AreNodesEquivalent(nextDivision.Dividend, firstLengthContext[i]) == false)
                {
                    return null;
                }
            }

            return firstLengthContext;
        }
    }
}
