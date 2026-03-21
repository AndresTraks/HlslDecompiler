using System;

namespace HlslDecompiler.DirectXShaderModel
{
    [Flags]
    public enum D3D10GlobalFlags
    {
        None = 0,
        RefactoringAllowed = 1,
        EnableDoublePrecisionFloatOps = 2,
        ForceEarlyDepthStencil = 4,
        EnableRawAndStructuredBuffers = 8,
        SkipOptimization = 16,
        EnableMinimumPrecision = 32,
        EnableDoubleExtensions = 64,
        EnableShaderExtensions = 128,
        AllResourcesBound = 256
    }
}
