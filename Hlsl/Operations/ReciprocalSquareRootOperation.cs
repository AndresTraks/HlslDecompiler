namespace HlslDecompiler.Hlsl
{
    public class ReciprocalSquareRootOperation : Operation
    {
        public ReciprocalSquareRootOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "rsqrt";
    }
}
