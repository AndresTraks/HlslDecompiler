namespace HlslDecompiler.Hlsl;

/// <summary>
/// `v[i]` with a run-time index, which fxc writes as `dp4 dst, v, icb[i]` over an
/// identity matrix in the immediate constant buffer.
/// </summary>
public class VectorComponentNode : Operation
{
    public VectorComponentNode(GroupNode vector, HlslTreeNode index)
    {
        AddInput(vector);
        AddInput(index);
    }

    public override string Mnemonic => $"dp{Vector.Length}";

    public GroupNode Vector => (GroupNode)Inputs[0];

    public HlslTreeNode Index => Inputs[1];

    public override string ToString()
    {
        return $"{Vector}[{Index}]";
    }
}
