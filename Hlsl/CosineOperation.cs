namespace HlslDecompiler.Hlsl
{
    public class CosineOperation : Operation
    {
        public CosineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string ToString()
        {
            return $"cos({Children[0]})";
        }
    }
}
