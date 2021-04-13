namespace HlslDecompiler.Hlsl
{
    public class ReciprocalSquareRootOperation : ConsumerOperation
    {
        public ReciprocalSquareRootOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "rsqrt";
    }
}
