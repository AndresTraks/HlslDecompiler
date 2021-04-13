namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyReciprocalDivisionTemplate : NodeTemplate<MultiplyOperation>
    {
        private TemplateMatcher _templateMatcher;

        public MultiplyReciprocalDivisionTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply
                && multiply.Factor1 is DivisionOperation division
                && _templateMatcher.IsOne(division.Dividend);
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return new DivisionOperation(node.Factor2, (node.Factor1 as DivisionOperation).Divisor);
        }
    }
}
