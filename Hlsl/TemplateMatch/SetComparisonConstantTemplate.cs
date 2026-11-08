namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// An slt, sge or cmp whose condition no operand value changes: constants, or
/// `a < -abs(x)` with a constant a >= 0, which no x makes true (a NaN compares
/// false). The Xbox 360 compiler makes a zero as `sgt r, -|x|, 0`.
/// </summary>
public class SetComparisonConstantTemplate : NodeTemplate<Operation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is CompareOperation { Value: ConstantNode } || Evaluate(node) != null;
    }

    public override HlslTreeNode Reduce(Operation node)
    {
        if (node is CompareOperation compare)
        {
            return ((ConstantNode)compare.Value).Value >= 0 ? compare.GreaterEqualValue : compare.LessValue;
        }
        return new ConstantNode(Evaluate(node).Value ? 1 : 0);
    }

    private static bool? Evaluate(HlslTreeNode node)
    {
        switch (node)
        {
            case SignLessOperation { Inputs: [ConstantNode a, ConstantNode b] }:
                return a.Value < b.Value;
            case SignGreaterOrEqualOperation { Inputs: [ConstantNode a, ConstantNode b] }:
                return a.Value >= b.Value;
            case SignLessOperation { Inputs: [ConstantNode { Value: >= 0 }, NegateOperation { Value: AbsoluteOperation }] }:
                return false;
            default:
                return null;
        }
    }
}
