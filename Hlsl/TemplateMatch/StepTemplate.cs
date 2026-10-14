using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Names step(edge, x) = `x >= edge ? 1 : 0`, from SM3 sge or an SM4 movc over a
/// float ge. An integer comparison stays a ternary: step returns a float.
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

        // ge is always the float comparison (ige/uge are separate opcodes).
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
