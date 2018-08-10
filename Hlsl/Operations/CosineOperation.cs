namespace HlslDecompiler.Hlsl
{
    public class CosineOperation : UnaryOperation
    {
        public CosineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "cos";
    }
}
