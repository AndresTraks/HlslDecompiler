namespace HlslDecompiler.Hlsl
{
    public class LinearInterpolateOperation : Operation
    {
        public LinearInterpolateOperation(HlslTreeNode amount, HlslTreeNode value1, HlslTreeNode value2)
        {
            AddChild(amount);
            AddChild(value1);
            AddChild(value2);
        }

        public HlslTreeNode Amount => Children[0];
        public HlslTreeNode Value1 => Children[1];
        public HlslTreeNode Value2 => Children[2];
    }
}
