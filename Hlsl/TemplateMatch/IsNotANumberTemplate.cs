using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// isnan from `x != x` (`ne r0.x, r0.x, r0.x`). Float comparisons only, not
/// integer ones or if_z bit tests.
/// </summary>
public class IsNotANumberTemplate : NodeTemplate<ComparisonNode>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is ComparisonNode comparison
            && comparison.Comparison == IfComparison.NE
            && !comparison.IsInteger
            && !comparison.IsBitsTest
            && NodeGrouper.AreNodesEquivalent(comparison.Left, comparison.Right);
    }

    public override HlslTreeNode Reduce(ComparisonNode node)
    {
        return new IsNotANumberOperation(node.Left);
    }
}
