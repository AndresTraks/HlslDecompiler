using System;

namespace HlslDecompiler.DirectXShaderModel
{
    // D3DXSHADER
    [Flags]
    public enum ShaderFlags
    {
        Debug = 1,
        SkipValidation = 2,
        SkipOptimization = 4,
        RowMajor = 8,
        ColumnMajor = 0x10,
        PartialPrecision = 0x20,
        ForceVSSoftwareNoOpt = 0x40,
        ForcePSSoftwareNoOpt = 0x80,
        NoPreShader = 0x100,
        AvoidFlowControl = 0x200,
        PreferFlowControl = 0x400,
        EnableStrictness = 0x800,
        EnableBackwardsCompatibility = 0x1000,
        IEEEStrictness = 0x2000,
        OptimizationLevel0 = 0x4000,
        OptimizationLevel1 = 0,
        OptimizationLevel2 = 0xC000,
        OptimizationLevel3 = 0x8000,
        UseLegacyD3DX931Dll = 0x10000,
        Unknown = 0x20000000,
    }
}
