namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyOneTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyOneTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply &&
                (_templateMatcher.IsOne(multiply.Factor1) || _templateMatcher.IsOne(multiply.Factor2));
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return _templateMatcher.IsOne(node.Factor1)
                ? node.Factor2
                : node.Factor1;
        }
    }
}
