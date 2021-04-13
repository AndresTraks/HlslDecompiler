namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyNegativeOneTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyNegativeOneTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply &&
                (_templateMatcher.IsNegativeOne(multiply.Factor1) || _templateMatcher.IsNegativeOne(multiply.Factor2));
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return _templateMatcher.IsNegativeOne(node.Factor1)
                ? new NegateOperation(node.Factor2)
                : new NegateOperation(node.Factor1);
        }
    }
}
