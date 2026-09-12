using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts a floating point remainder back together.
///
/// There is no instruction for it, so fxc writes the fractional part of the
/// quotient with the sign of the quotient put back, scaled by the divisor:
/// `(q >= -q ? frac(abs(q)) : -frac(abs(q))) * y`, where q is x / y. That is what
/// the shader does and not remotely what it says.
/// </summary>
public class FloatingModuloTemplate : NodeTemplate<MultiplyOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply && TryReduce(multiply) != null;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return TryReduce(node);
    }

    private static HlslTreeNode TryReduce(MultiplyOperation multiply)
    {
        return TryReduce(multiply.Factor1, multiply.Factor2)
            ?? TryReduce(multiply.Factor2, multiply.Factor1);
    }

    private static HlslTreeNode TryReduce(HlslTreeNode signedFraction, HlslTreeNode divisor)
    {
        if (signedFraction is not MoveConditionalOperation select)
        {
            return null;
        }

        // The positive branch is taken when the quotient is not negative.
        if (select.Source2 is not NegateOperation negated
            || !ReferenceEquals(negated.Value, select.Source1))
        {
            return null;
        }

        if (select.Source1 is not FractionalOperation fraction
            || fraction.Value is not AbsoluteOperation absolute
            || absolute.Value is not DivisionOperation quotient)
        {
            return null;
        }

        if (!ReferenceEquals(quotient.Divisor, divisor)
            || !IsNotNegative(select.Condition, quotient))
        {
            return null;
        }

        return new FloatingModuloOperation(quotient.Dividend, divisor);
    }

    /// <summary>
    /// `q >= -q` is how the sign of q is tested without naming a constant, which
    /// is what fxc writes here. A branch condition parses as a ComparisonNode and
    /// a value as a GreaterEqualOperation; this is the second.
    /// </summary>
    private static bool IsNotNegative(HlslTreeNode condition, HlslTreeNode value)
    {
        if (condition is GreaterEqualOperation greaterEqual)
        {
            return ReferenceEquals(greaterEqual.Source0, value)
                && IsNegationOf(greaterEqual.Source1, value);
        }
        return condition is ComparisonNode compare
            && compare.Comparison == IfComparison.GE
            && ReferenceEquals(compare.Left, value)
            && IsNegationOf(compare.Right, value);
    }

    private static bool IsNegationOf(HlslTreeNode node, HlslTreeNode value)
    {
        return node is NegateOperation negated && ReferenceEquals(negated.Value, value);
    }
}
