namespace HlslDecompiler.Hlsl
{
    public class CompareOperation : Operation
    {
        public CompareOperation(HlslTreeNode value, HlslTreeNode lessValue, HlslTreeNode greaterEqualValue)
        {
            AddInput(value);
            AddInput(lessValue);
            AddInput(greaterEqualValue);
        }

        public HlslTreeNode Value => Inputs[0];
        public HlslTreeNode LessValue => Inputs[1];
        public HlslTreeNode GreaterEqualValue => Inputs[2];

        public override string Mnemonic => "cmp";
    }
}
