using HlslDecompiler.DirectXShaderModel;
using System;

namespace HlslDecompiler.Hlsl;

public class ComparisonNode : HlslTreeNode
{
    public ComparisonNode(HlslTreeNode left, HlslTreeNode right, IfComparison comparison)
    {
        AddInput(left);
        AddInput(right);
        Comparison = comparison;
    }

    public HlslTreeNode Left => Inputs[0];
    public HlslTreeNode Right => Inputs[1];
    public IfComparison Comparison { get; }

    public override string ToString()
    {
        string comparison;
        switch (Comparison)
        {
            case IfComparison.GT: comparison = ">"; break;
            case IfComparison.EQ: comparison = "=="; break;
            case IfComparison.GE: comparison = ">="; break;
            case IfComparison.LT: comparison = "<"; break;
            case IfComparison.NE: comparison = "!="; break;
            case IfComparison.LE: comparison = "<="; break;
            default: throw new NotImplementedException(Comparison.ToString());
        }
        return $"{Left} {comparison} {Right}";
    }
}
