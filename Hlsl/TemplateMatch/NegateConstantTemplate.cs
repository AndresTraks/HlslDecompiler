namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class NegateConstantTemplate : NodeTemplate<NegateOperation>
    {
        private TemplateMatcher _templateMatcher;

        public NegateConstantTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is NegateOperation negate && _templateMatcher.IsConstant(negate.Value);
        }

        public override HlslTreeNode Reduce(NegateOperation node)
        {
            return new ConstantNode(-(node.Value as ConstantNode).Value);
        }
    }
}
