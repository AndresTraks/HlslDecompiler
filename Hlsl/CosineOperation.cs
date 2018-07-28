namespace HlslDecompiler.Hlsl
{
    public class CosineOperation : Operation
    {
        public CosineOperation(HlslTreeNode value)
        {
            AddChild(value);
        }

        public override string Mnemonic => "cos";
    }
}
