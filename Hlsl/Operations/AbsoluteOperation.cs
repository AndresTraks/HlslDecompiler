namespace HlslDecompiler.Hlsl
{
    public class AbsoluteOperation : UnaryOperation
    {
        public AbsoluteOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "abs";
    }
}
