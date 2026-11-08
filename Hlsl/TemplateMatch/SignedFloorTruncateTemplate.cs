namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers trunc (a cast to int) from floor(|x|) given x's sign,
/// `x >= 0 ? |x| - frac(|x|) : -(|x| - frac(|x|))`: the Xbox 360's trunc as
/// translated. Equal for every x, -0 and NaN included.
/// </summary>
public class SignedFloorTruncateTemplate : NodeTemplate<CompareOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is CompareOperation
            {
                GreaterEqualValue: SubtractOperation floor,
                LessValue: NegateOperation { Value: SubtractOperation negated },
            } compare
            && NodeGrouper.AreNodesEquivalent(floor, negated)
            && IsFloorOfAbsolute(floor, compare.Value);
    }

    public override HlslTreeNode Reduce(CompareOperation node)
    {
        return new TruncateOperation(node.Value);
    }

    // |x| - frac(|x|).
    private static bool IsFloorOfAbsolute(SubtractOperation floor, HlslTreeNode value)
    {
        return floor.Minuend is AbsoluteOperation { Value: var x }
            && NodeGrouper.AreNodesEquivalent(x, value)
            && floor.Subtrahend is FractionalOperation { Value: AbsoluteOperation { Value: var y } }
            && NodeGrouper.AreNodesEquivalent(y, value);
    }
}
