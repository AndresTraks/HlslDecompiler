namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyConstantsTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyConstantsTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply
                && _templateMatcher.IsConstant(multiply.Factor1)
                && _templateMatcher.IsConstant(multiply.Factor2);
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            var value = (node.Factor1 as ConstantNode).Value * (node.Factor2 as ConstantNode).Value;
            return new ConstantNode(value);
        }
    }
}
