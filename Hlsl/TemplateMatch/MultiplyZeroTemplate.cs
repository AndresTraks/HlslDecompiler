namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyZeroTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyZeroTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply &&
                (_templateMatcher.IsZero(multiply.Factor1) || _templateMatcher.IsZero(multiply.Factor2));
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return new ConstantNode(0);
        }
    }
}
