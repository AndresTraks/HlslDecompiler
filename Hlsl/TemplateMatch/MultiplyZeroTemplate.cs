namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyZeroTemplate : NodeTemplate<MultiplyOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply &&
                (ConstantMatcher.IsZero(multiply.Factor1) || ConstantMatcher.IsZero(multiply.Factor2));
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return new ConstantNode(0);
        }
    }
}
