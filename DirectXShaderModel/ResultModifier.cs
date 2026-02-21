using System;

namespace HlslDecompiler.DirectXShaderModel;

// https://docs.microsoft.com/en-us/windows-hardware/drivers/display/destination-parameter-token
[Flags]
public enum ResultModifier
{
    None = 0,
    Saturate = 1,
    PartialPrecision = 2,
    Centroid = 4
}
