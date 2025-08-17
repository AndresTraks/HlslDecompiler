using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class ComparePositiveAndNegativeTemplate : NodeTemplate<ComparisonNode>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is ComparisonNode comp &&
                ((comp.Left is NegateOperation negLeft && negLeft.Value == comp.Right)
                || (comp.Right is NegateOperation negRight && negRight.Value == comp.Left));
        }

        public override HlslTreeNode Reduce(ComparisonNode node)
        {
            if (node.Left is NegateOperation)
            {
                return new ComparisonNode(node.Right, new ConstantNode(0), IfComparison.NE);
            }
            return new ComparisonNode(node.Left, new ConstantNode(0), IfComparison.NE);
        }
    }
}
