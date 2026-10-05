using System;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts `rcp(max(x, k))` back together, which is how a shader divides by
/// something that might be nought.
///
/// `sum / max(total, 0.0001)` is the ordinary way to write a weighted average that
/// does not divide by zero, and in a profile with no divide instruction fxc writes
/// it as a reciprocal picked between by a compare:
///
///     add r0.x, r0.y, c11.w       total - 0.0001
///     rcp r0.y, r0.y              1 / total
///     cmp r0.x, r0.x, r0.y, c12.x total - 0.0001 &gt;= 0 ? 1 / total : 10000
///
/// Recovered as source that reads `(total - 0.0001 &gt;= 0 ? rcp(total) : 10000)
/// * sum`, where the 10000 is a magic number the reader has to divide back to see
/// the epsilon in. It is the same value as `rcp(max(total, 0.0001))` for every
/// input - above the epsilon the compare takes the reciprocal, and below it takes
/// the reciprocal of the epsilon, which is what the max would have given it - and
/// once it is a reciprocal of one thing, the multiply beside it is the division the
/// shader wrote. MultiplyReciprocalDivisionTemplate does that part.
///
/// The constant is checked against the reciprocal of the epsilon rather than taken
/// on trust, within a tolerance, because fxc folded it in floats: 1 / 0.0001 is
/// 10000.00025 and what it stored was 10000.
/// </summary>
public class GuardedReciprocalTemplate : NodeTemplate<CompareOperation>
{
    /// <summary>
    /// How far the stored reciprocal may be from the computed one, relative to it.
    /// Wide enough for a fold through floats and far too tight for two constants
    /// that are not reciprocals of each other to meet by accident.
    /// </summary>
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

    /// <summary>What is being divided by, and the floor under it.</summary>
    private static (HlslTreeNode Value, HlslTreeNode Epsilon)? TryGetValue(CompareOperation compare)
    {
        // The tested value is the number less the floor: `x - k >= 0` is `x >= k`.
        if (compare.Value is not SubtractOperation test
            || test.Subtrahend is not ConstantNode { IntegerValue: null } epsilon
            || epsilon.Value == 0)
        {
            return null;
        }
        // Taken when the test passes: the reciprocal of the number itself, which is
        // what makes this that number's division and not another's.
        if (compare.GreaterEqualValue is not ReciprocalOperation reciprocal
            || !ReferenceEquals(reciprocal.Value, test.Minuend))
        {
            return null;
        }
        // And when it fails: the reciprocal of the floor, as a number fxc folded.
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
