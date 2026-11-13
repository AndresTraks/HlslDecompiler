namespace HlslDecompiler.Hlsl;

/// <summary>
/// One component of one register of a blended matrix: the `A[r].c * w1 + B[r].c *
/// w2` that fxc computes per lane. Inputs[0] is the MatrixBlendNode, or the
/// variable naming made of it.
/// </summary>
public class MatrixRowNode : HlslTreeNode, IHasComponentIndex
{
    public MatrixRowNode(HlslTreeNode blend, DirectXShaderModel.ShaderTypeInfo matrixType, int register,
        int componentIndex)
    {
        AddInput(blend);
        MatrixType = matrixType;
        Register = register;
        ComponentIndex = componentIndex;
    }

    // The blended matrices' declared type, kept for when a variable is the input.
    public DirectXShaderModel.ShaderTypeInfo MatrixType { get; }

    public HlslTreeNode Blend => Inputs[0];

    // The register within the matrix, as the blended matrices are laid out.
    public int Register { get; }

    public int ComponentIndex { get; }

    public override string ToString()
    {
        return $"{Blend}[{Register}].{"xyzw"[ComponentIndex]}";
    }
}
