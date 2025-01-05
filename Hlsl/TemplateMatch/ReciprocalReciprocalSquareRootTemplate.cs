namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class ReciprocalReciprocalSquareRootTemplate : NodeTemplate<ReciprocalOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is ReciprocalOperation reciprocal
                && reciprocal.Value is DivisionOperation division
                && ConstantMatcher.IsOne(division.Dividend);
        }

        public override HlslTreeNode Reduce(ReciprocalOperation node)
        {
            var division = node.Value as DivisionOperation;
            return division.Divisor;
        }
    }
}
