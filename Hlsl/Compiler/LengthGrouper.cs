namespace HlslDecompiler.Hlsl
{
    public class LengthGrouper
    {
        private readonly NodeGrouper _nodeGrouper;

        public LengthGrouper(NodeGrouper nodeGrouper)
        {
            _nodeGrouper = nodeGrouper;
        }

        public HlslTreeNode[] TryGetLengthContext(HlslTreeNode node)
        {
            if (!(node is SquareRootOperation squareRoot))
            {
                return null;
            }

            DotProductContext dotProduct = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(squareRoot.Value);
            if (dotProduct == null)
            {
                return null;
            }

            if (NodeGrouper.IsVectorEquivalent(dotProduct.Value1, dotProduct.Value2) == false)
            {
                return null;
            }

            return dotProduct.Value1;
        }
    }
}
