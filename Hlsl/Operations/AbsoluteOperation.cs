namespace HlslDecompiler.Hlsl
{
    public class AbsoluteOperation : UnaryOperation
    {
        public AbsoluteOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "abs";
    }
}
