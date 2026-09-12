using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts sign back together.
///
/// There is no sign instruction, so fxc writes it as the difference of two
/// comparisons against zero - and a comparison writes all ones, so the difference
/// of the two masks is -1, 0 or 1 exactly. Recovered as source it reads as
/// `(t &lt; 0 ? -1 : 0) - (t &gt; 0 ? -1 : 0)`, which is what the shader does but
/// not what it says, and fxc does not recognise it as its own expansion coming
/// back, so it emits the selects rather than folding them away.
/// </summary>
public class SignTemplate : NodeTemplate<SubtractOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is SubtractOperation subtract && TryGetValue(subtract) != null;
    }

    public override HlslTreeNode Reduce(SubtractOperation node)
    {
        return new SignOperation(TryGetValue(node));
    }

    private static HlslTreeNode TryGetValue(SubtractOperation subtract)
    {
        HlslTreeNode negative = TryGetComparisonWithZero(subtract.Minuend, IfComparison.LT);
        if (negative == null)
        {
            return null;
        }

        HlslTreeNode positive = TryGetComparisonWithZero(subtract.Subtrahend, IfComparison.GT);
        return positive != null && ReferenceEquals(negative, positive) ? negative : null;
    }

    /// <returns>
    /// What is being compared, when it is compared with zero the given way round.
    /// Either side may hold the zero, so `0 &lt; t` counts as `t &gt; 0`.
    /// </returns>
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
