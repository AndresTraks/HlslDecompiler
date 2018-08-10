namespace HlslDecompiler.Hlsl
{
    public class FractionalOperation : UnaryOperation
    {
        public FractionalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "frc";
    }
}
