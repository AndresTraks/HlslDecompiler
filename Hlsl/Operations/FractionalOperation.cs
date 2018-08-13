namespace HlslDecompiler.Hlsl
{
    public class FractionalOperation : UnaryOperation
    {
        public FractionalOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "frc";
    }
}
