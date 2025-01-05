namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyNegativeOneTemplate : NodeTemplate<MultiplyOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply &&
                (ConstantMatcher.IsNegativeOne(multiply.Factor1) || ConstantMatcher.IsNegativeOne(multiply.Factor2));
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            return ConstantMatcher.IsNegativeOne(node.Factor1)
                ? new NegateOperation(node.Factor2)
                : new NegateOperation(node.Factor1);
        }
    }
}
