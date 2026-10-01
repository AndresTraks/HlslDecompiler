using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// isinf and isfinite, which fxc asks of the exponent field. An infinity is that
/// field at all ones over a zero fraction, so masking the sign away and comparing
/// the rest with 0x7f800000 is true of an infinity and of nothing else; keeping
/// the exponent alone and asking whether it is anything but all ones is true of
/// everything an infinity and a NaN are not, which is what finite means.
///
/// Both are integer comparisons of a mask against one constant, and the two
/// masks tell them apart: the sign stripped is 0x7fffffff, the exponent kept is
/// 0x7f800000. Nothing else asks either question, a shader testing its own
/// packed field having no reason to pick the exponent's own bits.
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
