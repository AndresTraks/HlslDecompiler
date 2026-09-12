namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts a signed divide or remainder back together.
///
/// The hardware divides unsigned, so a signed / or % compiles to the division of
/// the absolute values with the sign put back afterwards: negate the result when
/// the operands disagree in sign for a divide, or when the dividend is negative
/// for a remainder. Recovering that correction as source is right but unhelpful -
/// it reads as five operations instead of one, and fxc applies its own correction
/// around the / and % it then sees, so the shader pays for it twice.
/// </summary>
public class SignedDivideTemplate : NodeTemplate<MoveConditionalOperation>
{
    /// <summary>The sign bit, which is what the correction tests.</summary>
    private const int SignBit = unchecked((int)0x80000000);

    public override bool Match(HlslTreeNode node)
    {
        return node is MoveConditionalOperation move && TryReduce(move) != null;
    }

    public override HlslTreeNode Reduce(MoveConditionalOperation node)
    {
        return TryReduce(node);
    }

    private static HlslTreeNode TryReduce(MoveConditionalOperation move)
    {
        // The negated result is taken when the tested bit is set, so it is Source1.
        if (move.Source1 is not NegateOperation negate
            || !ReferenceEquals(negate.Value, move.Source2))
        {
            return null;
        }

        HlslTreeNode tested = TryGetSignTest(move.Condition);
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

    /// <returns>What the sign bit was taken of, or null if this is not that test.</returns>
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
    /// Rebuilds the signed operation, but only when the correction is the one that
    /// belongs to it: a divide is negated when the operands disagree in sign, which
    /// is the sign of their exclusive or, and a remainder follows the dividend
    /// alone. A shader correcting by anything else is doing something of its own.
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

    // The same node, or two constants that say the same thing. A constant divisor
    // reaches the division and the sign test as separate nodes, so the test would
    // not be recognised by identity alone.
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
