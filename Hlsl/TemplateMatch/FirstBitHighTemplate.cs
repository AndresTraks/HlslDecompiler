using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers firstbithigh from `31 - instruction` plus the select fxc adds to map
/// "no bit" (0xffffffff) back to -1. The parser writes the instruction as
/// `31 - firstbithigh(x)`, so the match is `31 - (31 - firstbithigh(x))` under the
/// guard. Unsigned tests x != 0; signed tests the instruction's result == -1.
/// Not matched when the subtraction sits in a variable over a structured load:
/// TempResolver refuses to resolve loads.
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
        // Signed: `instruction == -1 ? -1 : 31 - instruction`, one instruction node.
        if (ConstantMatcher.IsNegativeOne(select.Source1)
            && Position(select.Source2) is FirstBitHighOperation signedHigh
            && select.Condition is ComparisonNode { Comparison: IfComparison.EQ } test
            && ConstantMatcher.IsNegativeOne(test.Right)
            && ReferenceEquals(Instruction(test.Left), signedHigh))
        {
            return new FirstBitHighOperation(signedHigh.Value, signedHigh.IsUnsigned);
        }

        // Unsigned: `x ? 31 - instruction : -1`.
        if (ConstantMatcher.IsNegativeOne(select.Source2)
            && Position(select.Source1) is FirstBitHighOperation { IsUnsigned: true } unsignedHigh
            && TestsNonZero(select.Condition, unsignedHigh.Value))
        {
            return new FirstBitHighOperation(unsignedHigh.Value, isUnsigned: true);
        }

        return null;
    }

    private static bool TestsNonZero(HlslTreeNode condition, HlslTreeNode value)
    {
        if (condition is ComparisonNode { Comparison: IfComparison.NE } test
            && ConstantMatcher.IsZero(test.Right))
        {
            condition = test.Left;
        }
        return NodeGrouper.AreNodesEquivalent(condition, value);
    }

    /// <summary>`31 - instruction`: the function's answer.</summary>
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
