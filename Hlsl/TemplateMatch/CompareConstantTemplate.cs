using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class CompareConstantTemplate : NodeTemplate<ComparisonNode>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is ComparisonNode comp &&
                ConstantMatcher.IsConstant(comp.Left) && !ConstantMatcher.IsConstant(comp.Right);
        }

        public override HlslTreeNode Reduce(ComparisonNode node)
        {
            var comparison = node.Comparison switch
            {
                IfComparison.GT => IfComparison.LT,
                IfComparison.GE => IfComparison.LE,
                IfComparison.LT => IfComparison.GT,
                IfComparison.LE => IfComparison.GE,
                IfComparison.EQ => IfComparison.EQ,
                IfComparison.NE => IfComparison.NE,
                _ => throw new InvalidOperationException(node.Comparison.ToString()),
            };
            return new ComparisonNode(node.Right, node.Left, comparison);
        }
    }
}
