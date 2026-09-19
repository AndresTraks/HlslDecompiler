namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Puts `lerp` back together.
///
/// Shader model 3 has an lrp instruction and the parser builds this node from it
/// directly; DXBC has none, so fxc writes `lerp(a, b, s)` as `s * (b - a) + a` -
/// a subtract and a mad. That is the definition HLSL gives, and written out it is
/// three operations the reader has to put back together to see one.
/// </summary>
public class LinearInterpolateTemplate : NodeTemplate<AddOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is AddOperation add && TryGetContext(add) != null;
    }

    public override HlslTreeNode Reduce(AddOperation node)
    {
        var (amount, to, from) = TryGetContext(node).Value;
        return new LinearInterpolateOperation(amount, to, from);
    }

    private static (HlslTreeNode Amount, HlslTreeNode To, HlslTreeNode From)? TryGetContext(
        AddOperation add)
    {
        return TryGetContext(add.Addend1, add.Addend2) ?? TryGetContext(add.Addend2, add.Addend1);
    }

    // `amount * (to - from)` beside the same `from` it took away.
    private static (HlslTreeNode Amount, HlslTreeNode To, HlslTreeNode From)? TryGetContext(
        HlslTreeNode scaled, HlslTreeNode from)
    {
        if (scaled is not MultiplyOperation multiply)
        {
            return null;
        }
        if (multiply.Factor2 is SubtractOperation difference2
            && ReferenceEquals(difference2.Subtrahend, from))
        {
            return (multiply.Factor1, difference2.Minuend, from);
        }
        return multiply.Factor1 is SubtractOperation difference1
            && ReferenceEquals(difference1.Subtrahend, from)
            ? (multiply.Factor2, difference1.Minuend, from)
            : null;
    }
}
