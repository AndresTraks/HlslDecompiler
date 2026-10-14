namespace HlslDecompiler.DirectXShaderModel;

// D3D10_SB_RESOURCE_DIMENSION
public enum ResourceDimension
{
    Unknown,
    Buffer,
    Texture1D,
    Texture2D,
    Texture2Dms,
    Texture3D,
    TextureCube,
    Texture1DArray,
    Texture2DArray,
    Texture2DmsArray,
    TextureCubeArray,
    // Only in SM5 instruction extended tokens; declarations use opcodes.
    RawBuffer,
    StructuredBuffer
}
