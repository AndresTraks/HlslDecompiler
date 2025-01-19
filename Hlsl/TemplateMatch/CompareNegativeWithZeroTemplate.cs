using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.Hlsl.TemplateMatch
{
    public class CompareNegativeWithZeroTemplate : NodeTemplate<ComparisonNode>
    {
        public override bool Match(HlslTreeNode node)
        {
            return node is ComparisonNode comp &&
                comp.Left is NegateOperation && ConstantMatcher.IsZero(comp.Right);
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
            return new ComparisonNode((node.Left as NegateOperation).Value, node.Right, comparison);
        }
    }
}
