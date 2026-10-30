namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers trunc (a cast to int) from SM3's expansion
/// `(x &lt; 0) * (frac(x) &gt; 0) + floor(x)`, the comparisons as slt.
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

    /// <summary>slt(-frac(x), frac(x)), which is frac(x) &gt; 0 as one or nought.</summary>
    private static bool IsFractionTest(HlslTreeNode node, HlslTreeNode value)
    {
        return node is SignLessOperation less
            && less.Inputs[1] is FractionalOperation fraction
            && (ReferenceEquals(fraction.Value, value) || ParsedIdioms.DifferByWholeNumber(fraction.Value, value))
            && IsNegationOf(less.Inputs[0], fraction);
    }

    private static bool IsNegationOf(HlslTreeNode node, HlslTreeNode value)
    {
        return node is NegateOperation negate && ReferenceEquals(negate.Value, value);
    }
}
