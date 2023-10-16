namespace HlslDecompiler.Hlsl
{
    public class PartialDerivativeYOperation : ConsumerOperation
    {
        public PartialDerivativeYOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "ddy";
    }
}
