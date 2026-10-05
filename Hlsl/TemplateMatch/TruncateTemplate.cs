namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts trunc back together, which is what a cast to int is.
///
/// Shader model 3 has no truncation instruction, and rounding towards zero is not
/// what the address register does either - a move into it rounds to nearest - so
/// fxc writes `(int)x` out in full: the floor, plus one where the number is
/// negative and had a fraction to lose. Four instructions and two of them
/// comparisons:
///
///     slt r0.x, v0.x, -v0.x      x &lt; 0
///     frc r0.y, v0.x             frac(x)
///     add r0.z, -r0.y, v0.x      x - frac(x), which FloorTemplate reads as floor
///     slt r0.y, -r0.y, r0.y      frac(x) &gt; 0
///     mad r0.x, r0.x, r0.y, r0.z
///
/// Recovered as source that reads `((x &lt; -x) ? 1 : 0) * ((-frac(x) &lt;
/// frac(x)) ? 1 : 0) + floor(x)`, which is exactly what the shader does and
/// nothing like what it says - and the thing it usually says is `bones[(int)i]`.
///
/// floor plus the negative correction is trunc for every finite number: for x at
/// or above zero the correction is nought and both are floor, and below it the
/// floor is one too low wherever there is a fraction to lose, which is where the
/// correction is one.
/// </summary>
public class TruncateTemplate : NodeTemplate<AddOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AddOperation add && TryGetValue(add) != null;
    }

    public override HlslTreeNode Reduce(AddOperation node)
    {
        return new TruncateOperation(TryGetValue(node));
    }

    // Either way round: an add says nothing about which side fxc put the mad's
    // product on.
    private static HlslTreeNode TryGetValue(AddOperation add)
    {
        return TryGetValue(add.Addend1, add.Addend2)
            ?? TryGetValue(add.Addend2, add.Addend1);
    }

    private static HlslTreeNode TryGetValue(HlslTreeNode correction, HlslTreeNode flooring)
    {
        if (flooring is not FloorOperation floor
            || correction is not MultiplyOperation multiply)
        {
            return null;
        }
        HlslTreeNode value = floor.Value;
        // And either way round again, a multiply saying no more than the add does.
        return (IsNegativeTest(multiply.Factor1, value) && IsFractionTest(multiply.Factor2, value))
            || (IsNegativeTest(multiply.Factor2, value) && IsFractionTest(multiply.Factor1, value))
            ? value
            : null;
    }

    /// <summary>slt(x, -x), which is x &lt; 0 as one or nought.</summary>
    private static bool IsNegativeTest(HlslTreeNode node, HlslTreeNode value)
    {
        return node is SignLessOperation less
            && ReferenceEquals(less.Inputs[0], value)
            && IsNegationOf(less.Inputs[1], value);
    }

    /// <summary>slt(-frac(x), frac(x)), which is frac(x) &gt; 0 as one or nought.
    /// The same frac the floor was taken with, which is the one fxc computed.
    /// </summary>
    private static bool IsFractionTest(HlslTreeNode node, HlslTreeNode value)
    {
        return node is SignLessOperation less
            && less.Inputs[1] is FractionalOperation fraction
            && ReferenceEquals(fraction.Value, value)
            && IsNegationOf(less.Inputs[0], fraction);
    }

    private static bool IsNegationOf(HlslTreeNode node, HlslTreeNode value)
    {
        return node is NegateOperation negate && ReferenceEquals(negate.Value, value);
    }
}
