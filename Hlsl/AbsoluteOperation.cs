namespace HlslDecompiler.Hlsl
{
    public class AbsoluteOperation : Operation
    {
        public AbsoluteOperation(HlslTreeNode value)
        {
            AddChild(value);
        }
    }
}
