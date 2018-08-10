namespace HlslDecompiler.Hlsl
{
    public class SineOperation : UnaryOperation
    {
        public SineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "sin";
    }
}
