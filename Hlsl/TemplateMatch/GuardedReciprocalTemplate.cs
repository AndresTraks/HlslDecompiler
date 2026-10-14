using System;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers `rcp(max(x, k))` from fxc's SM3 form
/// `x - k &gt;= 0 ? rcp(x) : 1/k`; MultiplyReciprocalDivisionTemplate then makes
/// the division. The folded 1/k is checked within a tolerance.
/// </summary>
public class GuardedReciprocalTemplate : NodeTemplate<CompareOperation>
{
    /// <summary>Relative tolerance for fxc's float-folded 1/k.</summary>
    private const float Tolerance = 1e-5f;

    public override bool Match(HlslTreeNode node)
    {
        return node is CompareOperation compare && TryGetValue(compare) != null;
    }

    public override HlslTreeNode Reduce(CompareOperation node)
    {
        (HlslTreeNode value, HlslTreeNode epsilon) = TryGetValue(node).Value;
        return new ReciprocalOperation(new MaximumOperation(value, epsilon));
    }

    private static (HlslTreeNode Value, HlslTreeNode Epsilon)? TryGetValue(CompareOperation compare)
    {
        if (compare.Value is not SubtractOperation test
            || test.Subtrahend is not ConstantNode { IntegerValue: null } epsilon
            || epsilon.Value == 0)
        {
            return null;
        }
        if (compare.GreaterEqualValue is not ReciprocalOperation reciprocal
            || !ReferenceEquals(reciprocal.Value, test.Minuend))
        {
            return null;
        }
        if (compare.LessValue is not ConstantNode { IntegerValue: null } guarded)
        {
            return null;
        }
        float expected = 1f / epsilon.Value;
        return Math.Abs(guarded.Value - expected) <= Tolerance * Math.Abs(expected)
            ? (test.Minuend, epsilon)
            : null;
    }
}
