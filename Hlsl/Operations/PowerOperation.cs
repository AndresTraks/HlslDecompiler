namespace HlslDecompiler.Hlsl
{
    public class PowerOperation : Operation
    {
        public PowerOperation(HlslTreeNode value, HlslTreeNode power)
        {
            AddChild(value);
            AddChild(power);
        }

        public HlslTreeNode Value => Children[0];
        public HlslTreeNode Power => Children[1];

        public override string Mnemonic => "pow";
    }
}
