namespace HlslDecompiler.Hlsl.TemplateMatch;

/// <summary>
/// `asdouble(d, asuint(d).y)`: a double's own two words read back as a double is
/// the double. A loop-carried double's high word is its own phi, so the pair
/// arrives as the variable and its high word.
/// </summary>
public class DoubleRejoinTemplate : NodeTemplate<BitsToDoubleOperation>
{
    public override bool Match(HlslTreeNode node)
    {
        return node is BitsToDoubleOperation join && Rejoined(join) != null;
    }

    public override HlslTreeNode Reduce(BitsToDoubleOperation node)
    {
        return Rejoined(node);
    }

    private static HlslTreeNode Rejoined(BitsToDoubleOperation join)
    {
        if (join.High is not DoubleBitsNode { ComponentIndex: 1 } high)
        {
            return null;
        }
        HlslTreeNode value = high.Value;
        bool lowIsSame = ReferenceEquals(join.Low, value)
            || (join.Low is DoubleBitsNode { ComponentIndex: 0 } low && ReferenceEquals(low.Value, value));
        return lowIsSame && value is TempVariableNode { IsDouble: true } ? value : null;
    }
}
