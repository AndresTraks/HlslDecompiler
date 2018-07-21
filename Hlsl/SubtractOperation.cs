namespace HlslDecompiler.Hlsl
{
    public class SubtractOperation : Operation
    {
        public SubtractOperation(HlslTreeNode minuend, HlslConstant subtrahend)
        {
            AddChild(minuend);
            AddChild(subtrahend);
        }

        public HlslTreeNode Minuend => Children[0];
        public HlslTreeNode Subtrahend => Children[1];
    }
}
