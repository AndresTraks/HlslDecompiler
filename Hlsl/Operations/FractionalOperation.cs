namespace HlslDecompiler.Hlsl
{
    public class FractionalOperation : ConsumerOperation
    {
        public FractionalOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "frc";
    }
}
