namespace HlslDecompiler.Hlsl
{
    public class ReciprocalOperation : Operation
    {
        public ReciprocalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string ToString()
        {
            return $"rcp({Children[0]})";
        }
    }
}
