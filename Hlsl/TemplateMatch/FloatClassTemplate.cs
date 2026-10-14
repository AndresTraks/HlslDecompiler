using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// isinf: `(x &amp; 0x7fffffff) == 0x7f800000`; isfinite:
/// `(x &amp; 0x7f800000) != 0x7f800000`. The mask tells them apart.
/// </summary>
public class FloatClassTemplate : NodeTemplate<ComparisonNode>
{
    private const int WithoutSign = 0x7FFFFFFF;
    private const int Exponent = 0x7F800000;

    public override bool Match(HlslTreeNode node)
    {
        return Reduce(node as ComparisonNode) != null;
    }

    public override HlslTreeNode Reduce(ComparisonNode node)
    {
        if (node == null
            || !node.IsInteger
            || node.IsBitsTest
            || node.Left is not BitwiseAndOperation masked
            || AsInt(node.Right) != Exponent)
        {
            return null;
        }
        int? mask = AsInt(masked.Value2) ?? AsInt(masked.Value1);
        HlslTreeNode value = AsInt(masked.Value2) != null ? masked.Value1 : masked.Value2;
        if (node.Comparison == IfComparison.EQ && mask == WithoutSign)
        {
            return new IsInfiniteOperation(value);
        }
        if (node.Comparison == IfComparison.NE && mask == Exponent)
        {
            return new IsFiniteOperation(value);
        }
        return null;
    }

    private static int? AsInt(HlslTreeNode node)
    {
        return (node as ConstantNode)?.IntegerValue;
    }
}
