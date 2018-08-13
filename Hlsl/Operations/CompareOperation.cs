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

        public override string Mnemonic => "cmp";
    }
}
