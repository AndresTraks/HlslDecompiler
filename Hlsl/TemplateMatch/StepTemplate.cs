using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts step back together.
///
/// step(edge, x) is defined as `x >= edge ? 1 : 0` and compiles to exactly
/// that, so nothing is being recovered here beyond the name: shader model 3
/// has the instruction as sge, and shader model 4 writes the comparison with a
/// movc choosing between the two constants. Both come back out as a ternary,
/// which says what the hardware does rather than what the shader asked for, and
/// costs a pair of brackets everywhere it appears as an operand.
///
/// An integer comparison stays a ternary. step returns a float, and a mask
/// tested on integers is usually on its way into arithmetic that would go
/// floating with it.
/// </summary>
public class StepTemplate : NodeTemplate<Operation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is Operation operation && TryReduce(operation) != null;
    }

    public override HlslTreeNode Reduce(Operation node)
    {
        return TryReduce(node);
    }

    private static HlslTreeNode TryReduce(Operation operation)
    {
        // sge(a, b) is a >= b, which is step(b, a): the edge is the second operand.
        if (operation is SignGreaterOrEqualOperation)
        {
            return new StepOperation(operation.Inputs[1], operation.Inputs[0]);
        }

        if (operation is not MoveConditionalOperation select
            || !ConstantMatcher.IsOne(select.Source1)
            || !ConstantMatcher.IsZero(select.Source2))
        {
            return null;
        }

        // ge writes its own node rather than a ComparisonNode, so the shape the
        // shader model 4 mask arrives in is a select over one of those. It is the
        // float comparison - ige and uge are separate opcodes - so there is nothing
        // further to ask about the operands.
        if (select.Condition is GreaterEqualOperation greaterEqual)
        {
            return new StepOperation(greaterEqual.Source1, greaterEqual.Source0);
        }

        if (select.Condition is not ComparisonNode comparison
            || comparison.Comparison != IfComparison.GE
            || comparison.IsInteger)
        {
            return null;
        }
        return new StepOperation(comparison.Right, comparison.Left);
    }
}
