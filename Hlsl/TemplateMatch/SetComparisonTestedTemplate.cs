using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// An slt or sge result tested against zero, `((a < b) ? 1 : 0) != 0`, is the
/// comparison itself: the Xbox 360 sets its predicate from one. Only `!= 0`:
/// `== 0` holds for a NaN, where the opposite comparison does not.
/// </summary>
public class SetComparisonTestedTemplate : NodeTemplate<ComparisonNode>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is ComparisonNode
        {
            Comparison: IfComparison.NE,
            Left: SignLessOperation or SignGreaterOrEqualOperation,
            IsInteger: false,
        } comparison && ConstantMatcher.IsZero(comparison.Right);
    }

    public override HlslTreeNode Reduce(ComparisonNode node)
    {
        var set = (Operation)node.Left;
        return new ComparisonNode(set.Inputs[0], set.Inputs[1],
            set is SignLessOperation ? IfComparison.LT : IfComparison.GE);
    }
}
