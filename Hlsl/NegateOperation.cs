namespace HlslDecompiler.Hlsl
{
    public class NegateOperation : Operation
    {
        public NegateOperation(HlslTreeNode value)
            : base(OperationType.Negate)
        {
            AddChild(value);
        }
    }
}
