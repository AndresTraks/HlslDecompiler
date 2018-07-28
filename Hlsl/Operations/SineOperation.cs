namespace HlslDecompiler.Hlsl
{
    public class SineOperation : Operation
    {
        public SineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "sin";
    }
}
