namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers floor from SM3's `x - frac(x)`; left as is, fxc floors a subscript
/// a second time.
/// </summary>
public class FloorTemplate : NodeTemplate<SubtractOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is SubtractOperation subtract && TryGetValue(subtract) != null;
    }

    public override HlslTreeNode Reduce(SubtractOperation node)
    {
        return new FloorOperation(TryGetValue(node));
    }

    private static HlslTreeNode TryGetValue(SubtractOperation subtract)
    {
        return subtract.Subtrahend is FractionalOperation fraction
            && ReferenceEquals(fraction.Value, subtract.Minuend)
            ? subtract.Minuend
            : null;
    }
}
