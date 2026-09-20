using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.Hlsl.TemplateMatch;

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
        // Carrying what the node knew: the same two values compared the same way
        // round the other way about, so it is still the integer comparison it was,
        // and still the unsigned one. Dropped, an unsigned test swapped to put its
        // constant on the right came out signed.
        return new ComparisonNode(
            node.Right, node.Left, comparison, node.IsInteger, node.IsUnsigned);
    }
}
