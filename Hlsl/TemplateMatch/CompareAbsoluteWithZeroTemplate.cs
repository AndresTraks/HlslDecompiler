using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// cmp tests its value against zero, and -abs(x) reaches zero only where x is
/// zero, so a cmp over one is asking whether x is one particular number.
/// `-abs(address) >= 0 ? 1 : 0` says that the way the bytecode has to; the source
/// it came from said `address == 0`.
///
/// The comparison templates never see this one, because a cmp carries no
/// comparison node - the `>= 0` is put there by the writer when it compiles the
/// operation - so the rewrite belongs at the cmp itself.
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
