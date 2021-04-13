namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class ReciprocalReciprocalSquareRootTemplate : NodeTemplate<ReciprocalOperation>
    {
        private TemplateMatcher _templateMatcher;

        public ReciprocalReciprocalSquareRootTemplate(TemplateMatcher templateMatcher)
        {
            _templateMatcher = templateMatcher;
        }

        public override bool Match(HlslTreeNode node)
        {
            return node is ReciprocalOperation reciprocal
                && reciprocal.Value is DivisionOperation division
                && _templateMatcher.IsOne(division.Dividend);
        }

        public override HlslTreeNode Reduce(ReciprocalOperation node)
        {
            var division = node.Value as DivisionOperation;
            return division.Divisor;
        }
    }
}
