namespace HlslDecompiler.Hlsl
{
    public class SineOperation : Operation
    {
        public SineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string ToString()
        {
            return $"sin({Children[0]})";
        }
    }
}
