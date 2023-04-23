using System;

namespace HlslDecompiler.DirectXShaderModel
{
    // D3DXSHADER
    [Flags]
    enum ShaderFlags
    {
        Debug = 1,
        SkipValidation = 2,
        SkipOptimization = 4,
        RowMajor = 8,
        ColumnMajor = 16,
        PartialPrecision = 32,
        ForceVSSoftwareNoOpt = 64,
        ForcePSSoftwareNoOpt = 128,
        NoPreShader = 256,
        AvoidFlowControl = 512,
        PreferFlowControl = 1024,
        EnableStrictness = 2048,
        EnableBackwardsCompatibility = 4096,
        IEEEStrictness = 8192,
        OptimizationLevel0 = 16384,
        OptimizationLevel1 = 0,
        OptimizationLevel2 = 49152,
        OptimizationLevel3 = 32768,
        UseLegacyD3DX931Dll = 65536
    }
}
