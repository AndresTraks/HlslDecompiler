namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class MultiplyConstantsTemplate : NodeTemplate<MultiplyOperation>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is MultiplyOperation multiply
                && ConstantMatcher.IsConstant(multiply.Factor1)
                && ConstantMatcher.IsConstant(multiply.Factor2);
        }

        public override HlslTreeNode Reduce(MultiplyOperation node)
        {
            var value = (node.Factor1 as ConstantNode).Value * (node.Factor2 as ConstantNode).Value;
            return new ConstantNode(value);
        }
    }
}
