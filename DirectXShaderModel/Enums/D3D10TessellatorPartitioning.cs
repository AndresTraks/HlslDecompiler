namespace HlslDecompiler.DirectXShaderModel;

// D3D_TESSELLATOR_PARTITIONING; HLSL [partitioning(...)], hull shaders only.
public enum D3D10TessellatorPartitioning
{
    Undefined,
    Integer,
    Pow2,
    FractionalOdd,
    FractionalEven,
}
