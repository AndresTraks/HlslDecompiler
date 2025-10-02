namespace HlslDecompiler.Hlsl
{
    public class GreaterEqualOperation : Operation
    {
        public GreaterEqualOperation(HlslTreeNode source0, HlslTreeNode source1)
        {
            AddInput(source0);
            AddInput(source1);
        }

        public HlslTreeNode Source0 => Inputs[1];
        public HlslTreeNode Source1 => Inputs[2];

        public override string Mnemonic => "ge";
    }
}
