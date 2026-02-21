namespace HlslDecompiler.DirectXShaderModel;

// D3D_SHADER_INPUT_TYPE
public enum D3DShaderInputType
{
    CBuffer,
    TBuffer,
    Texture,
    Sampler,
    UavRWTyped,
    Structured,
    UavRWStructured,
    ByteAddress,
    UavRWByteAddress,
    UavAppendStructured,
    UavConsumeStructured,
    UavRWStucturedWithCounter,
    RTAccelerationStructure,
    UavFeedbackTexture
}
