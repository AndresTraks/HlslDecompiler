namespace HlslDecompiler.Hlsl;

public static class AssociativityTester
{
    public static bool TestForMultiplication(HlslTreeNode node)
    {
        switch (node)
        {
            case AddOperation _:
            case SubtractOperation _:
                return false;
            default:
                return !NeedsParenthesesAsOperand(node);
        }
    }

    // Operators that bind more loosely than the arithmetic they sit inside, so they
    // need parentheses wherever they appear as an operand. Without them
    // `a + (c ? 1 : 0)` prints as `a + c ? 1 : 0`, which groups the addition into
    // the condition and computes something else entirely.
    public static bool NeedsParenthesesAsOperand(HlslTreeNode node)
    {
        switch (node)
        {
            case MoveConditionalOperation _:
            case LogicalAndOperation _:
            case LogicalOrOperation _:
            case CompareOperation _:
            case ComparisonNode _:
            case GreaterEqualOperation _:
            // Shift and the bitwise operators bind more loosely than the arithmetic
            // around them.
            case ShiftLeftOperation _:
            case BitwiseAndOperation _:
            case BitwiseOrOperation _:
            case BitwiseXorOperation _:
            // sge and slt also compile to a ternary.
            case SignGreaterOrEqualOperation _:
            case SignLessOperation _:
                return true;
            default:
                return false;
        }
    }
}
