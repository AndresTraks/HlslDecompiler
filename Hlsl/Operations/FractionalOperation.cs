namespace HlslDecompiler.Hlsl
{
    public class FractionalOperation : Operation
    {
        public FractionalOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "frc";
    }
}
