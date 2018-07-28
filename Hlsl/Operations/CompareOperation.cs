namespace HlslDecompiler.Hlsl
{
    public class CompareOperation : Operation
    {
        public CompareOperation(HlslTreeNode value, HlslTreeNode lessValue, HlslTreeNode greaterEqualValue)
        {
            AddChild(value);
            AddChild(lessValue);
            AddChild(greaterEqualValue);
        }

        public override string Mnemonic => "cmp";
    }
}
