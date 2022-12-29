using System;

namespace HlslDecompiler.DirectXShaderModel
{
    [Flags]
    public enum D3D10OperandModifier
    {
        None = 0,
        Neg = 1,
        Abs = 2
    }
}
