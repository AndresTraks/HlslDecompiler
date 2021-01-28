namespace HlslDecompiler.Hlsl
{
    public class AbsoluteOperation : ConsumerOperation
    {
        public AbsoluteOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "abs";
    }
}
