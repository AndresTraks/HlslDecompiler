using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// `-abs(x) >= 0 ? a : b` is `x == 0 ? a : b`. Matched on the cmp itself, which
/// has no comparison node for the comparison templates to see.
/// </summary>
public class CompareAbsoluteWithZeroTemplate : NodeTemplate<CompareOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is CompareOperation { Value: NegateOperation { Value: AbsoluteOperation } };
    }

    public override HlslTreeNode Reduce(CompareOperation node)
    {
        HlslTreeNode value = ((NegateOperation)node.Value).Value.Inputs[0];
        return new MoveConditionalOperation(
            new ComparisonNode(value, new ConstantNode(0), IfComparison.EQ),
            node.GreaterEqualValue,
            node.LessValue);
    }
}
