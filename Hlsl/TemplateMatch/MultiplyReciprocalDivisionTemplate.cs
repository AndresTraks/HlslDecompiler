namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyReciprocalDivisionTemplate : NodeTemplate<MultiplyOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply
                && multiply.Factor1 is DivisionOperation division
                && ConstantMatcher.IsOne(division.Dividend);
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return new DivisionOperation(node.Factor2, (node.Factor1 as DivisionOperation).Divisor);
        }
    }
}
