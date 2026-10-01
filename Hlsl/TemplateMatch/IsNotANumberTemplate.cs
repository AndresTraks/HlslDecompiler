using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// A NaN is the only float that is not equal to itself, so fxc asks whether a
/// value is one by comparing it with itself - `ne r0.x, r0.x, r0.x` - and needs
/// no constant to do it. Nothing else is written that way: for every other float
/// the comparison is false, so a shader that meant it would be writing a zero.
///
/// Only of floats. Two integers compared that way are never unequal, and the
/// bits test an if_z carries is no comparison of a value with anything.
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
