namespace HlslDecompiler.Hlsl
{
    public class ReciprocalSquareRootOperation : UnaryOperation
    {
        public ReciprocalSquareRootOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "rsqrt";
    }
}
