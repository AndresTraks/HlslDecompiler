using HlslDecompiler.DirectXShaderModel;

namespace HlslDecompiler.Hlsl;

public class ComparisonNode : HlslTreeNode
{
    public ComparisonNode(
        HlslTreeNode left,
        HlslTreeNode right,
        IfComparison comparison,
        bool isInteger = false,
        bool isUnsigned = false)
    {
        AddInput(left);
        AddInput(right);
        Comparison = comparison;
        IsInteger = isInteger;
        IsUnsigned = isUnsigned;
    }

    public HlslTreeNode Left => Inputs[0];
    public HlslTreeNode Right => Inputs[1];
    public IfComparison Comparison { get; }

    /// <summary>Compared as integers (ilt, not lt).</summary>
    public bool IsInteger { get; }

    /// <summary>
    /// Compared as unsigned (ult); HLSL takes signedness from the operands, so one
    /// must be cast.
    /// </summary>
    public bool IsUnsigned { get; }

    /// <summary>
    /// An if_z/if_nz bit test for zero, not a comparison; it must not type its
    /// operand as float.
    /// </summary>
    public bool IsBitsTest { get; init; }

    /// <summary>The opposite comparison, or null if there is none.</summary>
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
            : new ComparisonNode(Left, Right, inverted, IsInteger, IsUnsigned) { IsBitsTest = IsBitsTest };
    }

    public override string ToString()
    {
        return $"{Left} {Comparison.ToHlslString()} {Right}";
    }
}
