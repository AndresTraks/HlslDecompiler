namespace HlslDecompiler.Hlsl
{
    public abstract class UnaryOperation : Operation
    {
        public HlslTreeNode Value => Children[0];
    }
}
