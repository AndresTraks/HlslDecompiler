using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

public class ComparisonNode : HlslTreeNode
{
    public ComparisonNode(
        HlslTreeNode left, HlslTreeNode right, IfComparison comparison, bool isInteger = false)
    {
        AddInput(left);
        AddInput(right);
        Comparison = comparison;
        IsInteger = isInteger;
    }

    public HlslTreeNode Left => Inputs[0];
    public HlslTreeNode Right => Inputs[1];
    public IfComparison Comparison { get; }

    /// <summary>
    /// Whether the two sides were compared as integers. ilt and lt both read as
    /// `a &lt; b`, so without this the mask a comparison writes cannot be told
    /// apart from the bits of a float.
    /// </summary>
    public bool IsInteger { get; }

    /// <summary>The same comparison with the opposite outcome, or null for one
    /// that has none - a stale loop counter test, say.</summary>
    public ComparisonNode Inverted()
    {
        IfComparison inverted = Comparison switch
        {
            IfComparison.GT => IfComparison.LE,
            IfComparison.GE => IfComparison.LT,
            IfComparison.LT => IfComparison.GE,
            IfComparison.LE => IfComparison.GT,
            IfComparison.EQ => IfComparison.NE,
            IfComparison.NE => IfComparison.EQ,
            _ => IfComparison.None,
        };
        return inverted == IfComparison.None
            ? null
            : new ComparisonNode(Left, Right, inverted, IsInteger);
    }

    public override string ToString()
    {
        return $"{Left} {Comparison.ToHlslString()} {Right}";
    }
}
