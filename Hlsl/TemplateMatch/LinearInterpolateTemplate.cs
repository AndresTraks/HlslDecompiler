namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// Recovers lerp(a, b, s) from DXBC's `s * (b - a) + a` (SM3 has lrp).
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
        // A `to` of 0 was folded away: `amount * -from + from`.
        if (multiply.Factor2 is NegateOperation negated2 && ReferenceEquals(negated2.Value, from))
        {
            return (multiply.Factor1, new ConstantNode(0f), from);
        }
        if (multiply.Factor1 is NegateOperation negated1 && ReferenceEquals(negated1.Value, from))
        {
            return (multiply.Factor2, new ConstantNode(0f), from);
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
