namespace HlslDecompiler.Hlsl
{
    public class PowerOperation : Operation
    {
        public PowerOperation(HlslTreeNode value, HlslTreeNode power)
        {
            AddInput(value);
            AddInput(power);
        }

        public HlslTreeNode Value => Inputs[0];
        public HlslTreeNode Power => Inputs[1];

        public override string Mnemonic => "pow";
    }
}
