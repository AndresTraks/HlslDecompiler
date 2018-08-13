namespace HlslDecompiler.Hlsl
{
    public class SquareRootOperation : UnaryOperation
    {
        public SquareRootOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "sqrt";
    }
}
