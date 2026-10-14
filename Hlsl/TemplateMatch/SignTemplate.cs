using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers sign from fxc's difference of two comparison masks,
/// `(t &lt; 0) - (t &gt; 0)` with all-ones masks, or SM3's
/// `slt(-t, t) - slt(t, -t)`.
/// </summary>
public class SignTemplate : NodeTemplate<SubtractOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is SubtractOperation subtract && TryGetValue(subtract) != null;
    }

    public override HlslTreeNode Reduce(SubtractOperation node)
    {
        (HlslTreeNode value, bool negated) = TryGetValue(node).Value;
        var sign = new SignOperation(value);
        return negated ? new NegateOperation(sign) : (HlslTreeNode)sign;
    }

    /// <summary>
    /// The value, and whether the masks are reversed: fxc writes `-sign(t)` by
    /// swapping the comparisons.
    /// </summary>
    private static (HlslTreeNode Value, bool Negated)? TryGetValue(SubtractOperation subtract)
    {
        HlslTreeNode negative = TryGetComparisonWithZero(subtract.Minuend, IfComparison.LT);
        if (negative != null)
        {
            HlslTreeNode positive = TryGetComparisonWithZero(subtract.Subtrahend, IfComparison.GT);
            return positive != null && ReferenceEquals(negative, positive)
                ? (negative, false)
                : null;
        }

        HlslTreeNode reversedPositive = TryGetComparisonWithZero(subtract.Minuend, IfComparison.GT);
        if (reversedPositive != null)
        {
            HlslTreeNode reversedNegative = TryGetComparisonWithZero(
                subtract.Subtrahend, IfComparison.LT);
            return reversedNegative != null && ReferenceEquals(reversedPositive, reversedNegative)
                ? (reversedPositive, true)
                : null;
        }

        HlslTreeNode signLess = TryGetSignLessValue(subtract);
        return signLess == null ? null : (signLess, false);
    }

    // slt(-t, t) is t > 0 and slt(t, -t) is t < 0, each as 1 or 0.
    private static HlslTreeNode TryGetSignLessValue(SubtractOperation subtract)
    {
        if (subtract.Minuend is not SignLessOperation positive
            || subtract.Subtrahend is not SignLessOperation negative)
        {
            return null;
        }
        HlslTreeNode value = positive.Inputs[1];
        return IsNegationOf(positive.Inputs[0], value)
            && ReferenceEquals(negative.Inputs[0], value)
            && IsNegationOf(negative.Inputs[1], value)
            ? value
            : null;
    }

    private static bool IsNegationOf(HlslTreeNode node, HlslTreeNode value)
    {
        return node is NegateOperation negate && ReferenceEquals(negate.Value, value);
    }

    /// <returns>t in `t op 0`, also accepting `0 reverse(op) t`.</returns>
    private static HlslTreeNode TryGetComparisonWithZero(HlslTreeNode node, IfComparison comparison)
    {
        if (node is not ComparisonNode compare)
        {
            return null;
        }
        if (ConstantMatcher.IsZero(compare.Right) && compare.Comparison == comparison)
        {
            return compare.Left;
        }
        if (ConstantMatcher.IsZero(compare.Left) && compare.Comparison == Reverse(comparison))
        {
            return compare.Right;
        }
        return null;
    }

    private static IfComparison Reverse(IfComparison comparison)
    {
        return comparison switch
        {
            IfComparison.LT => IfComparison.GT,
            IfComparison.GT => IfComparison.LT,
            IfComparison.LE => IfComparison.GE,
            IfComparison.GE => IfComparison.LE,
            _ => comparison,
        };
    }
}
