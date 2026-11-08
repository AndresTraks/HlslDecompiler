using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// An slt or sge result, always exactly 0 or 1, selecting 0 or 1 by whether it
/// is 0, `(((a < b) ? 1 : 0) == 0) ? 0 : 1`, is the result itself: the Xbox 360
/// keeps its predicate as such a 0/1 snapshot.
/// </summary>
public class SetComparisonSelectedTemplate : NodeTemplate<MoveConditionalOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        if (node is not MoveConditionalOperation
            {
                Condition: ComparisonNode
                {
                    Comparison: IfComparison.EQ or IfComparison.NE,
                    Left: SignLessOperation or SignGreaterOrEqualOperation,
                    IsInteger: false,
                } condition,
            } select
            || !ConstantMatcher.IsZero(condition.Right))
        {
            return false;
        }
        // Zero where the result is zero, one where it is not.
        (HlslTreeNode zero, HlslTreeNode one) = condition.Comparison == IfComparison.EQ
            ? (select.Source1, select.Source2)
            : (select.Source2, select.Source1);
        return ConstantMatcher.IsZero(zero) && ConstantMatcher.IsOne(one);
    }

    public override HlslTreeNode Reduce(MoveConditionalOperation node)
    {
        return ((ComparisonNode)node.Condition).Left;
    }
}
