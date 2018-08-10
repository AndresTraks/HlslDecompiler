namespace HlslDecompiler.Hlsl
{
    public class SquareRootOperation : UnaryOperation
    {
        public SquareRootOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "sqrt";
    }
}
