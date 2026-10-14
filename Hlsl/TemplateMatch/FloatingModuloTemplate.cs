using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers fmod(x, y) from fxc's expansion
/// `(q >= -q ? frac(abs(q)) : -frac(abs(q))) * y`, q = x / y, in its movc and
/// SM3 cmp + rcp forms. The quotient or rcp may sit in a hoisted variable, read
/// through the matcher's TempResolver.
/// </summary>
public class FloatingModuloTemplate : NodeTemplate<MultiplyOperation>
{
    private readonly TemplateMatcher _templateMatcher;

    public FloatingModuloTemplate(TemplateMatcher templateMatcher)
    {
        _templateMatcher = templateMatcher;
    }

    public override bool Match(HlslTreeNode node)
    {
        return node is MultiplyOperation multiply && TryReduce(multiply) != null;
    }

    public override HlslTreeNode Reduce(MultiplyOperation node)
    {
        return TryReduce(node);
    }

    private HlslTreeNode TryReduce(MultiplyOperation multiply)
    {
        return TryReduce(multiply.Factor1, multiply.Factor2)
            ?? TryReduce(multiply.Factor2, multiply.Factor1);
    }

    private HlslTreeNode TryReduce(HlslTreeNode signedFraction, HlslTreeNode scale)
    {
        if (signedFraction is MoveConditionalOperation select
            && select.Source2 is NegateOperation negated
            && ReferenceEquals(negated.Value, select.Source1))
        {
            if (select.Source1 is not FractionalOperation fraction
                || fraction.Value is not AbsoluteOperation absolute
                || !TrySplitQuotient(absolute.Value, out HlslTreeNode dividend, out HlslTreeNode divisor)
                || !IsNotNegative(select.Condition, absolute.Value))
            {
                return null;
            }
            return Remainder(dividend, divisor, scale);
        }

        // SM3 cmp: the same sign selection with the test against zero built in.
        if (signedFraction is CompareOperation cmp
            && IsNegationOf(cmp.LessValue, cmp.GreaterEqualValue)
            && cmp.GreaterEqualValue is FractionalOperation cmpFraction
            && cmpFraction.Value is AbsoluteOperation cmpAbsolute
            && ReferenceEquals(cmpAbsolute.Value, cmp.Value)
            && TrySplitQuotient(cmp.Value, out HlslTreeNode cmpDividend, out HlslTreeNode cmpDivisor))
        {
            return Remainder(cmpDividend, cmpDivisor, scale);
        }

        // fxc folds `y - fmod(x, y)` into `mad(-s, y, y)`; the negation belongs
        // to the subtraction and moves outside the remainder.
        if (signedFraction is NegateOperation negatedFraction
            && TryReduce(negatedFraction.Value, scale) is HlslTreeNode negatedRemainder)
        {
            return new NegateOperation(negatedRemainder);
        }

        return null;
    }

    /// <summary>
    /// The remainder, once the scale is seen to be the quotient's divisor.
    /// </summary>
    private static HlslTreeNode Remainder(
        HlslTreeNode dividend, HlslTreeNode divisor, HlslTreeNode scale)
    {
        // Equivalence, not identity: a modifier like `-k.x` is a fresh node at each read.
        if (NodeGrouper.AreNodesEquivalent(divisor, scale))
        {
            return new FloatingModuloOperation(dividend, scale);
        }

        // fmod takes the dividend's sign, so a negated scale is `-fmod(x, y)`.
        if (scale is NegateOperation negatedScale
            && NodeGrouper.AreNodesEquivalent(negatedScale.Value, divisor))
        {
            return new NegateOperation(new FloatingModuloOperation(dividend, negatedScale.Value));
        }

        // A negated divisor in the rcp only: `-fmod(x, -y)`.
        if (divisor is NegateOperation negatedDivisor
            && NodeGrouper.AreNodesEquivalent(negatedDivisor.Value, scale))
        {
            return new NegateOperation(new FloatingModuloOperation(dividend, divisor));
        }

        return null;
    }

    /// <summary>
    /// Splits x / y or x * rcp(y), looking through variables via the resolver.
    /// </summary>
    private bool TrySplitQuotient(
        HlslTreeNode quotient, out HlslTreeNode dividend, out HlslTreeNode divisor)
    {
        switch (Resolve(quotient))
        {
            case DivisionOperation division:
                dividend = division.Dividend;
                divisor = division.Divisor;
                return true;
            case MultiplyOperation multiply
                when Resolve(multiply.Factor1) is ReciprocalOperation reciprocal1:
                dividend = multiply.Factor2;
                divisor = reciprocal1.Value;
                return true;
            case MultiplyOperation multiply
                when Resolve(multiply.Factor2) is ReciprocalOperation reciprocal2:
                dividend = multiply.Factor1;
                divisor = reciprocal2.Value;
                return true;
        }
        dividend = null;
        divisor = null;
        return false;
    }

    private HlslTreeNode Resolve(HlslTreeNode node) =>
        _templateMatcher.TempResolver is { } resolver ? resolver.Resolve(node) : node;

    /// <summary>
    /// `q >= -q` or `q >= 0`, as a GreaterEqualOperation value or a ComparisonNode
    /// branch condition.
    /// </summary>
    private static bool IsNotNegative(HlslTreeNode condition, HlslTreeNode value)
    {
        if (condition is GreaterEqualOperation greaterEqual)
        {
            return ReferenceEquals(greaterEqual.Source0, value)
                && (IsNegationOf(greaterEqual.Source1, value)
                    || ConstantMatcher.IsZero(greaterEqual.Source1));
        }
        return condition is ComparisonNode compare
            && compare.Comparison == IfComparison.GE
            && ReferenceEquals(compare.Left, value)
            && (IsNegationOf(compare.Right, value)
                || ConstantMatcher.IsZero(compare.Right));
    }

    private static bool IsNegationOf(HlslTreeNode node, HlslTreeNode value)
    {
        return node is NegateOperation negated && ReferenceEquals(negated.Value, value);
    }
}
