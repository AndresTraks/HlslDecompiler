namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyConstantTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyConstantTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply
                && !_templateMatcher.IsConstant(multiply.Factor1)
                && _templateMatcher.IsConstant(multiply.Factor2);
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return new MultiplyOperation(node.Factor2, node.Factor1);
        }
    }
}
