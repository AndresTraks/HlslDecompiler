namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of a vector, picked by an index worked out while the shader runs.
/// No instruction reads a register at a computed component, so fxc writes an
/// identity matrix into the immediate constant buffer and dots the vector with the
/// row the index selects: `v[i]` is `dp4 dst, v, icb[i]`. Read back as the dot it
/// is, the subscript the shader was written with comes out as four lines of
/// literals and an arithmetic identity standing in for it.
/// </summary>
public class VectorComponentNode : Operation
{
    public VectorComponentNode(GroupNode vector, HlslTreeNode index)
    {
        AddInput(vector);
        AddInput(index);
    }

    // The instruction it stands for, which is as wide as the vector it reads.
    public override string Mnemonic => $"dp{Vector.Length}";

    public GroupNode Vector => (GroupNode)Inputs[0];

    public HlslTreeNode Index => Inputs[1];

    public override string ToString()
    {
        return $"{Vector}[{Index}]";
    }
}
