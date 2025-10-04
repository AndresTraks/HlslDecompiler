using System;

namespace HlslDecompiler.DirectXShaderModel
{
    // D3D_SHADER_VARIABLE_FLAGS
    [Flags]
    public enum D3D10ShaderVariableFlags
    {
        None = 0,
        UserPacked = 1,
        Used = 2,
        InterfacePointer = 4,
        InterfaceParameter = 8
    }
}
