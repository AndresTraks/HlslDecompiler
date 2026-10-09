using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts firstbithigh back together.
///
/// The instruction counts the bits above the highest one and the function
/// counts from below it, so the function is `31 - instruction` - and where
/// there is no bit to find the two disagree about more than the direction: the
/// instruction answers 0xffffffff, and 31 minus that is 32 rather than the -1
/// the function promises. So fxc writes the subtraction and then a select that
/// puts the -1 back, and asks a different question in each form: the unsigned
/// one tests the value, which has no bit to find only when it is zero, and the
/// signed one tests the instruction's own answer, since a word that is all sign
/// - 0 or -1 - is the case there.
///
/// The parser writes each instruction as the subtraction it is, so both idioms
/// arrive here as arithmetic over a firstbithigh, and matching one is matching
/// `31 - (31 - firstbithigh(x))` with the guard around it.
///
/// The unsigned form is matched where the select reads the subtraction itself:
/// `x ? 31 - (31 - firstbithigh(x)) : -1`, fxc having folded the register away.
/// What stops it otherwise is narrower than "a temp variable". fxc computes the subtraction into a register of its
/// own and selects over that register, so the select's arm is a variable - which
/// TempResolver exists to see through, and FloatingModuloTemplate does see
/// through. It will not see through this one: the value is
/// `31 - (31 - firstbithigh(load))`, TempResolver.IsReadable refuses anything
/// holding a LoadStructuredNode, and so the resolver has no value recorded for
/// the variable at all.
///
/// That guard is there to stop a fold duplicating a buffer load, and it does not
/// quite fit here - this fold would leave the load read once where the output now
/// reads it twice - but bypassing it is a change to what every template may
/// duplicate, for the sake of one shader's second component. Tried 2026-10-03:
/// resolving in this template alone changes nothing, because the resolver has
/// nothing to give it. Written out as the arithmetic it is, which is correct and
/// says less: cs_5_0/high_bit's `31 - (31 - firstbithigh((uint)(t0 + k.x)))`.
/// </summary>
public class FirstBitHighTemplate : NodeTemplate<MoveConditionalOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is MoveConditionalOperation select && TryReduce(select) != null;
    }

    public override HlslTreeNode Reduce(MoveConditionalOperation node)
    {
        return TryReduce(node);
    }

    private static HlslTreeNode TryReduce(MoveConditionalOperation select)
    {
        // `instruction == -1 ? -1 : 31 - instruction`, the signed form. The same
        // instruction node is read twice, which is what makes this the idiom rather
        // than two comparisons that happen to agree.
        if (ConstantMatcher.IsNegativeOne(select.Source1)
            && Position(select.Source2) is FirstBitHighOperation signedHigh
            && select.Condition is ComparisonNode { Comparison: IfComparison.EQ } test
            && ConstantMatcher.IsNegativeOne(test.Right)
            && ReferenceEquals(Instruction(test.Left), signedHigh))
        {
            return new FirstBitHighOperation(signedHigh.Value, signedHigh.IsUnsigned);
        }

        // `x ? 31 - instruction : -1`, the unsigned form: no bit to find only when
        // x is zero, and that is the select's question.
        if (ConstantMatcher.IsNegativeOne(select.Source2)
            && Position(select.Source1) is FirstBitHighOperation { IsUnsigned: true } unsignedHigh
            && TestsNonZero(select.Condition, unsignedHigh.Value))
        {
            return new FirstBitHighOperation(unsignedHigh.Value, isUnsigned: true);
        }

        return null;
    }

    // Whether a condition is the value read as true where it is not zero.
    private static bool TestsNonZero(HlslTreeNode condition, HlslTreeNode value)
    {
        if (condition is ComparisonNode { Comparison: IfComparison.NE } test
            && ConstantMatcher.IsZero(test.Right))
        {
            condition = test.Left;
        }
        return NodeGrouper.AreNodesEquivalent(condition, value);
    }

    /// <summary>The instruction's answer read the way the function reads it.</summary>
    private static FirstBitHighOperation Position(HlslTreeNode node)
    {
        return node is SubtractOperation { Minuend: ConstantNode { Value: 31 } } subtract
            ? Instruction(subtract.Subtrahend)
            : null;
    }

    /// <summary>One firstbit_hi or firstbit_shi, as the parser writes it.</summary>
    private static FirstBitHighOperation Instruction(HlslTreeNode node)
    {
        return node is SubtractOperation { Minuend: ConstantNode { Value: 31 } } subtract
            && subtract.Subtrahend is FirstBitHighOperation high
            ? high
            : null;
    }
}
