using HlslDecompiler.DirectXShaderModel;

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
        return $"{Left} {Comparison.ToHlslString()} {Right}";
    }
}
