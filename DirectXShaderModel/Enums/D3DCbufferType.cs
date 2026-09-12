namespace HlslDecompiler.DirectXShaderModel;

// D3D_CBUFFER_TYPE
public enum D3DCbufferType
{
    Cbuffer,
    Tbuffer,
    InterfacePointers,
    // Not a buffer the shader reads: fxc emits one of these per structured
    // buffer, named after it, holding a variable called $Element whose type is
    // the element type. It is the only place that type appears.
    ResourceBindInfo
}
