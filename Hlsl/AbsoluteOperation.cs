namespace HlslDecompiler.Hlsl
{
    public class AbsoluteOperation : Operation
    {
        public AbsoluteOperation(HlslTreeNode value)
            : base(OperationType.Absolute)
        {
            AddChild(value);
        }
    }
}
