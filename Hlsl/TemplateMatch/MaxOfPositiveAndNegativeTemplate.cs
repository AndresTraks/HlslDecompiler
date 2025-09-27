namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MaxOfPositiveAndNegativeTemplate : NodeTemplate<MaximumOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is MaximumOperation max &&
                ((max.Value1 is NegateOperation neg1 && max.Value2 == neg1.Value) || (max.Value2 is NegateOperation neg2 && max.Value1 == neg2.Value));
        }

        public override HlslTreeNode Reduce(MaximumOperation max)
        {
            if (max.Value1 is NegateOperation neg1 && max.Value2 == neg1.Value)
            {
                return new AbsoluteOperation(max.Value2);
            }
            return new AbsoluteOperation(max.Value1);
        }
    }
}
