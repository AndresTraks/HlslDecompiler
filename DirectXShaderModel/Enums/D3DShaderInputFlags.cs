using System;

namespace HlslDecompiler.DirectXShaderModel
{
    // D3D_SHADER_INPUT_FLAGS
    [Flags]
    public enum D3DShaderInputFlags
    {
        None = 0,
        UserPacked = 1,
        ComparisonSampler = 2,
        TextureComponent0 = 4,
        TextureComponent1 = 8,
        Unused = 16
    }
}
