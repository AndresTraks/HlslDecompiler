namespace HlslDecompiler.Hlsl
{
    public class SubtractOperation : Operation
    {
        public SubtractOperation(HlslTreeNode minuend, HlslTreeNode subtrahend)
        {
            AddChild(minuend);
            AddChild(subtrahend);
        }

        public HlslTreeNode Minuend => Children[0];
        public HlslTreeNode Subtrahend => Children[1];

        public override string ToString()
        {
            return $"sub({Minuend}, {Subtrahend})";
        }
    }
}
