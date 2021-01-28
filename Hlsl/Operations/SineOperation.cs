namespace HlslDecompiler.Hlsl
{
    public class SineOperation : ConsumerOperation
    {
        public SineOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "sin";
    }
}
