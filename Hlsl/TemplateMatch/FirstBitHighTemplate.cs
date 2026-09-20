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
/// Only the signed form is matched. The unsigned one asks its question of the
/// value rather than of the answer, which leaves the subtraction with a reader
/// of its own and lands it in a temp variable before this runs - and a temp
/// variable is a name, with no way back to the value it was given. Written out
/// as the arithmetic it is, which is correct and says less.
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

        return null;
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
