namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : Operation
    {
        public NegateOperation(HlslTreeNode value)
        {
            AddChild(value);
        }
    }
}
