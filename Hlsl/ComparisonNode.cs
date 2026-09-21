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

    /// <summary>
    /// Whether the two sides were compared as integers. ilt and lt both read as
    /// `a &lt; b`, so without this the mask a comparison writes cannot be told
    /// apart from the bits of a float.
    /// </summary>
    public bool IsInteger { get; }

    /// <summary>
    /// Whether they were compared as unsigned integers. ult and ilt both read as
    /// `a &lt; b`, and HLSL takes the signedness from the operands rather than from
    /// the operator, so the unsigned form has to say so at one of them.
    /// </summary>
    public bool IsUnsigned { get; }

    /// <summary>
    /// Whether this is an if_z or if_nz over a register that is no comparison - a
    /// test of the bits for zero. It says nothing about what those bits are, so it
    /// types nothing: taken as a float comparison, the -1 a branch moved into a
    /// mask register was retyped as the NaN its bits are.
    /// </summary>
    public bool IsBitsTest { get; init; }

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
            : new ComparisonNode(Left, Right, inverted, IsInteger, IsUnsigned) { IsBitsTest = IsBitsTest };
    }

    public override string ToString()
    {
        return $"{Left} {Comparison.ToHlslString()} {Right}";
    }
}
