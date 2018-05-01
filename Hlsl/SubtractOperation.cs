namespace HlslDecompiler.Hlsl
{
    public class SubtractOperation : Operation
    {
        public SubtractOperation(HlslTreeNode minuend, HlslConstant subtrahend)
            : base(OperationType.Subtract)
        {
            AddChild(minuend);
            AddChild(subtrahend);
        }
    }
}
