namespace HlslDecompiler.Hlsl
{
    public class FractionalOperation : Operation
    {
        public FractionalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string ToString()
        {
            return $"frc({Children[0]})";
        }
    }
}
