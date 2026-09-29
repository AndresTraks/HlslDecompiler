using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts a floating point remainder back together.
///
/// There is no instruction for it, so fxc writes the fractional part of the
/// quotient with the sign of the quotient put back, scaled by the divisor:
/// `(q >= -q ? frac(abs(q)) : -frac(abs(q))) * y`, where q is x / y. That is what
/// the shader does and not remotely what it says.
///
/// Shader model 3 has neither a divide nor a comparison that reaches a register:
/// cmp puts the sign selection in one instruction and tests the quotient against
/// zero, and the quotient itself is what rcp and mul make of a divide. The test
/// is the same one - q >= -q holds just where q >= 0 - so both spellings of the
/// selection and of the quotient fold to the same remainder.
///
/// The rcp that makes a quotient is often not in the expression at all: fxc hoists
/// the loop-invariant half of a divide into a temp and reads that at every use, and
/// a variable is as much a wall to this template as a register is. It asks the
/// matcher's resolver what such a variable holds, and reads the quotient through
/// it.
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
        // A movc picks the sign from a comparison of the quotient against its own
        // negation, which the branch condition can still be seen to say.
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

        // A cmp tests its first operand against zero and takes the second or third
        // with it - the same sign selection, its test folded into the instruction.
        // What it tests is what the fraction took the absolute value of: the
        // quotient is read twice, not computed twice.
        if (signedFraction is CompareOperation cmp
            && IsNegationOf(cmp.LessValue, cmp.GreaterEqualValue)
            && cmp.GreaterEqualValue is FractionalOperation cmpFraction
            && cmpFraction.Value is AbsoluteOperation cmpAbsolute
            && ReferenceEquals(cmpAbsolute.Value, cmp.Value)
            && TrySplitQuotient(cmp.Value, out HlslTreeNode cmpDividend, out HlslTreeNode cmpDivisor))
        {
            return Remainder(cmpDividend, cmpDivisor, scale);
        }

        return null;
    }

    /// <summary>
    /// The remainder the signed fraction and the scale put together: the fraction's
    /// quotient divided by the scale's divisor, once they are seen to be the same.
    /// </summary>
    private static HlslTreeNode Remainder(
        HlslTreeNode dividend, HlslTreeNode divisor, HlslTreeNode scale)
    {
        // The divisor is read twice, once in the quotient and once as the scale,
        // and a modifier on it - `-k.x` - is a node of its own at each.
        if (NodeGrouper.AreNodesEquivalent(divisor, scale))
        {
            return new FloatingModuloOperation(dividend, scale);
        }

        // A remainder takes its sign from the dividend, not the divisor, so scaling
        // by the opposite of what was divided by is a negated fmod rather than one -
        // which is how fxc writes `-fmod(x, y)`: the negation joins the scale, and
        // the rcp keeps its sign.
        if (scale is NegateOperation negatedScale
            && NodeGrouper.AreNodesEquivalent(negatedScale.Value, divisor))
        {
            return new NegateOperation(new FloatingModuloOperation(dividend, negatedScale.Value));
        }

        // The same opposition with the negation on the other side of it: the rcp
        // carries it and the scale does not, which is what fxc makes of dividing
        // by something already negative - `-fmod(x, -y)`. The fmod keeps the
        // divisor its quotient was made with, so the sign is written where the
        // shader read it.
        if (divisor is NegateOperation negatedDivisor
            && NodeGrouper.AreNodesEquivalent(negatedDivisor.Value, scale))
        {
            return new NegateOperation(new FloatingModuloOperation(dividend, divisor));
        }

        return null;
    }

    /// <summary>
    /// The quotient as a dividend and a divisor. With a div instruction it is the
    /// instruction itself; without one it is x multiplied by the reciprocal of y,
    /// whichever side the reciprocal is on. Either the quotient or the reciprocal
    /// may be the value a variable holds rather than a node of the expression, and
    /// both are looked through where the resolver can see what the variable holds.
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
    /// `q >= -q` is how the sign of q is tested without naming a constant, which
    /// is what fxc writes when the comparison is an instruction of its own; `q >= 0`
    /// says the same and holds just as well, and is what a cmp leaves no room for
    /// spelling otherwise. A branch condition parses as a ComparisonNode and a
    /// value as a GreaterEqualOperation; this is the second.
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
