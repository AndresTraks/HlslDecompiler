namespace HlslDecompiler.DirectXShaderModel;

// D3D_TESSELLATOR_PARTITIONING: how the tessellator cuts an edge it has been given
// a factor for, which HLSL writes as the [partitioning(...)] attribute on a hull
// shader. A domain shader does not say it; only the shader that produces the
// factors does.
public enum D3D10TessellatorPartitioning
{
    Undefined,
    Integer,
    Pow2,
    FractionalOdd,
    FractionalEven,
}
