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
            if (!(node is ReciprocalOperation reciprocal))
            {
                return null;
            }

            if (!(reciprocal.Value is ReciprocalSquareRootOperation reciprocalSquareRoot))
            {
                return null;
            }

            DotProductContext dotProduct = _nodeGrouper.DotProductGrouper.TryGetDotProductGroup(reciprocalSquareRoot.Value);
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
