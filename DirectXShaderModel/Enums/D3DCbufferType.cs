namespace HlslDecompiler.DirectXShaderModel;

// D3D_CBUFFER_TYPE
public enum D3DCbufferType
{
    Cbuffer,
    Tbuffer,
    InterfacePointers,
    // One per structured buffer, named after it; its $Element variable is the
    // only record of the element type.
    ResourceBindInfo
}
