namespace HlslDecompiler.Hlsl
{
    public class ClipOperation : ConsumerOperation
    {
        public ClipOperation(HlslTreeNode value)
        {
            AddInput(value);
        }

        public override string Mnemonic => "clip";
    }
}
