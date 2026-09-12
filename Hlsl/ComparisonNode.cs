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

    public override string ToString()
    {
        return $"{Left} {Comparison.ToHlslString()} {Right}";
    }
}
