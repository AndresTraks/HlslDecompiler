namespace HlslDecompiler.Hlsl
{
    public class SignGreaterOrEqualOperation : Operation
    {
        public SignGreaterOrEqualOperation(HlslTreeNode value1, HlslTreeNode value2)
        {
            AddChild(value1);
            AddChild(value2);
        }

        public override string ToString()
        {
            return $"sge({Children[0]}, {Children[2]})";
        }
    }
}
