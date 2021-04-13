namespace HlslDecompiler.Hlsl
{
    public class AddOperation : Operation
    {
        public AddOperation(HlslTreeNode addend1, HlslTreeNode addend2)
        {
            AddInput(addend1);
            AddInput(addend2);
        }

        public HlslTreeNode Addend1 => Inputs[0];
        public HlslTreeNode Addend2 => Inputs[1];

        public override string Mnemonic => "add";
    }
}
