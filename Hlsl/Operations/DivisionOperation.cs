namespace HlslDecompiler.Hlsl
{
    public class DivisionOperation : Operation
    {
        public DivisionOperation(HlslTreeNode dividend, HlslTreeNode divisor)
        {
            AddChild(dividend);
            AddChild(divisor);
        }

        public HlslTreeNode Dividend => Children[0];
        public HlslTreeNode Divisor => Children[1];

        public override string Mnemonic => "div";
    }
}
