namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers signed / and %, which compile to an unsigned divide of absolute
/// values with the result negated by sign (of x ^ y for /, of x for %).
/// </summary>
public class SignedDivideTemplate : NodeTemplate<MoveConditionalOperation>
{
    private const int SignBit = unchecked((int)0x80000000);

    private readonly TemplateMatcher _templateMatcher;

    public SignedDivideTemplate(TemplateMatcher templateMatcher)
    {
        _templateMatcher = templateMatcher;
    }

    public override bool Match(HlslTreeNode node)
    {
        return node is MoveConditionalOperation move && TryReduce(move) != null;
    }

    public override HlslTreeNode Reduce(MoveConditionalOperation node)
    {
        return TryReduce(node);
    }

    private HlslTreeNode TryReduce(MoveConditionalOperation move)
    {
        // The negated result is taken when the tested bit is set, so it is Source1.
        if (move.Source1 is not NegateOperation negate
            || !ReferenceEquals(negate.Value, move.Source2))
        {
            return null;
        }

        // The sign test may sit in a variable with lanes of another and: fxc's
        // `and r1.xyzw, v1.yxzw, l(0x80000000, 3, 3, 0x80000000)` is named whole.
        HlslTreeNode tested = TryGetSignTest(Resolve(move.Condition));
        if (tested == null)
        {
            return null;
        }

        if (move.Source2 is DivisionOperation division)
        {
            return TryRebuild(tested, division.Dividend, division.Divisor, isDivision: true);
        }
        if (move.Source2 is ModuloOperation modulo)
        {
            return TryRebuild(tested, modulo.Dividend, modulo.Divisor, isDivision: false);
        }
        return null;
    }

    private HlslTreeNode Resolve(HlslTreeNode node) =>
        _templateMatcher.TempResolver is { } resolver ? resolver.Resolve(node) : node;

    /// <returns>x in `x &amp; 0x80000000`, or null.</returns>
    private static HlslTreeNode TryGetSignTest(HlslTreeNode condition)
    {
        if (condition is not BitwiseAndOperation and)
        {
            return null;
        }
        if (IsSignBit(and.Inputs[1]))
        {
            return and.Inputs[0];
        }
        return IsSignBit(and.Inputs[0]) ? and.Inputs[1] : null;
    }

    private static bool IsSignBit(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.IntegerValue == SignBit;
    }

    /// <summary>
    /// Rebuilds the signed operation only if the test is its own: x ^ y for a
    /// divide, x for a remainder.
    /// </summary>
    private static HlslTreeNode TryRebuild(
        HlslTreeNode tested,
        HlslTreeNode dividend,
        HlslTreeNode divisor,
        bool isDivision)
    {
        HlslTreeNode signedDividend = TryGetAbsoluteValue(dividend);
        if (signedDividend == null)
        {
            return null;
        }

        // A positive constant divisor has no absolute value left to take.
        HlslTreeNode signedDivisor = TryGetAbsoluteValue(divisor)
            ?? (IsPositiveConstant(divisor) ? divisor : null);
        if (signedDivisor == null)
        {
            return null;
        }

        if (isDivision)
        {
            if (!IsExclusiveOr(tested, signedDividend, signedDivisor))
            {
                return null;
            }
            return new DivisionOperation(signedDividend, signedDivisor);
        }

        if (!IsSameValue(tested, signedDividend))
        {
            return null;
        }
        return new ModuloOperation(signedDividend, signedDivisor);
    }

    private static HlslTreeNode TryGetAbsoluteValue(HlslTreeNode node)
    {
        return node is AbsoluteOperation absolute ? absolute.Value : null;
    }

    private static bool IsPositiveConstant(HlslTreeNode node)
    {
        return node is ConstantNode constant && constant.Value > 0;
    }

    private static bool IsExclusiveOr(HlslTreeNode node, HlslTreeNode left, HlslTreeNode right)
    {
        if (node is not BitwiseXorOperation exclusiveOr)
        {
            return false;
        }
        return (IsSameValue(exclusiveOr.Inputs[0], left)
                && IsSameValue(exclusiveOr.Inputs[1], right))
            || (IsSameValue(exclusiveOr.Inputs[0], right)
                && IsSameValue(exclusiveOr.Inputs[1], left));
    }

    // Constants compare by value: a constant divisor is a separate node at each use.
    private static bool IsSameValue(HlslTreeNode left, HlslTreeNode right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        return left is ConstantNode leftConstant
            && right is ConstantNode rightConstant
            && leftConstant.Value == rightConstant.Value
            && leftConstant.IntegerValue == rightConstant.IntegerValue;
    }
}
