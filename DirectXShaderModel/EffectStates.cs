using System.Collections.Generic;

namespace HlslDecompiler.DirectXShaderModel;

public enum EffectBlockType
{
    DepthStencil = 1,
    Blend,
    Rasterizer,
    Sampler,
    Pass,
}

public enum EffectStateValueType
{
    Object,
    Bool,
    UInt,
    UInt8,
    Float,
}

/// <summary>
/// A state an effect assigns: its name in HLSL, the block it is assigned in, what
/// it takes, how many of that (a colour takes four), the names its values have,
/// how many there are of it (one per render target, for most of a blend state),
/// and whether one value written without a subscript sets all of them.
/// </summary>
public sealed record EffectState(
    string Name,
    EffectBlockType Block,
    EffectStateValueType ValueType,
    int Columns = 1,
    IReadOnlyDictionary<uint, string> ValueNames = null,
    int Indices = 1,
    bool VectorScalar = false);

/// <summary>
/// The runtime's table of states, which an assignment indexes - g_lvGeneral in
/// FX11's EffectStates11.h, in the same order. The values a name stands for are
/// the Direct3D 11 enumerations', which the effect compiler resolves the names to.
/// </summary>
public static class EffectStates
{
    private static readonly Dictionary<uint, string> Bool = new() { [0] = "FALSE", [1] = "TRUE" };

    private static readonly Dictionary<uint, string> DepthWriteMask = new() { [0] = "ZERO", [1] = "ALL" };

    private static readonly Dictionary<uint, string> Fill = new() { [2] = "WIREFRAME", [3] = "SOLID" };

    private static readonly Dictionary<uint, string> Filter = new()
    {
        [0x00] = "MIN_MAG_MIP_POINT",
        [0x01] = "MIN_MAG_POINT_MIP_LINEAR",
        [0x04] = "MIN_POINT_MAG_LINEAR_MIP_POINT",
        [0x05] = "MIN_POINT_MAG_MIP_LINEAR",
        [0x10] = "MIN_LINEAR_MAG_MIP_POINT",
        [0x11] = "MIN_LINEAR_MAG_POINT_MIP_LINEAR",
        [0x14] = "MIN_MAG_LINEAR_MIP_POINT",
        [0x15] = "MIN_MAG_MIP_LINEAR",
        [0x55] = "ANISOTROPIC",
        [0x80] = "COMPARISON_MIN_MAG_MIP_POINT",
        [0x81] = "COMPARISON_MIN_MAG_POINT_MIP_LINEAR",
        [0x84] = "COMPARISON_MIN_POINT_MAG_LINEAR_MIP_POINT",
        [0x85] = "COMPARISON_MIN_POINT_MAG_MIP_LINEAR",
        [0x90] = "COMPARISON_MIN_LINEAR_MAG_MIP_POINT",
        [0x91] = "COMPARISON_MIN_LINEAR_MAG_POINT_MIP_LINEAR",
        [0x94] = "COMPARISON_MIN_MAG_LINEAR_MIP_POINT",
        [0x95] = "COMPARISON_MIN_MAG_MIP_LINEAR",
        [0xD5] = "COMPARISON_ANISOTROPIC",
    };

    private static readonly Dictionary<uint, string> Blend = new()
    {
        [1] = "ZERO",
        [2] = "ONE",
        [3] = "SRC_COLOR",
        [4] = "INV_SRC_COLOR",
        [5] = "SRC_ALPHA",
        [6] = "INV_SRC_ALPHA",
        [7] = "DEST_ALPHA",
        [8] = "INV_DEST_ALPHA",
        [9] = "DEST_COLOR",
        [10] = "INV_DEST_COLOR",
        [11] = "SRC_ALPHA_SAT",
        [14] = "BLEND_FACTOR",
        [15] = "INV_BLEND_FACTOR",
        [16] = "SRC1_COLOR",
        [17] = "INV_SRC1_COLOR",
        [18] = "SRC1_ALPHA",
        [19] = "INV_SRC1_ALPHA",
    };

    private static readonly Dictionary<uint, string> TextureAddress = new()
    {
        [1] = "WRAP",
        [2] = "MIRROR",
        [3] = "CLAMP",
        [4] = "BORDER",
        [5] = "MIRROR_ONCE",
    };

    private static readonly Dictionary<uint, string> Cull = new() { [1] = "NONE", [2] = "FRONT", [3] = "BACK" };

    private static readonly Dictionary<uint, string> Comparison = new()
    {
        [1] = "NEVER",
        [2] = "LESS",
        [3] = "EQUAL",
        [4] = "LESS_EQUAL",
        [5] = "GREATER",
        [6] = "NOT_EQUAL",
        [7] = "GREATER_EQUAL",
        [8] = "ALWAYS",
    };

    private static readonly Dictionary<uint, string> StencilOp = new()
    {
        [1] = "KEEP",
        [2] = "ZERO",
        [3] = "REPLACE",
        [4] = "INCR_SAT",
        [5] = "DECR_SAT",
        [6] = "INVERT",
        [7] = "INCR",
        [8] = "DECR",
    };

    private static readonly Dictionary<uint, string> BlendOp = new()
    {
        [1] = "ADD",
        [2] = "SUBTRACT",
        [3] = "REV_SUBTRACT",
        [4] = "MIN",
        [5] = "MAX",
    };

    public static readonly IReadOnlyList<EffectState> All =
    [
        new("RasterizerState", EffectBlockType.Pass, EffectStateValueType.Object),
        new("DepthStencilState", EffectBlockType.Pass, EffectStateValueType.Object),
        new("BlendState", EffectBlockType.Pass, EffectStateValueType.Object),
        new("RenderTargetView", EffectBlockType.Pass, EffectStateValueType.Object, Indices: 8),
        new("DepthStencilView", EffectBlockType.Pass, EffectStateValueType.Object, Indices: 8),
        new("GenerateMips", EffectBlockType.Pass, EffectStateValueType.Object),
        new("VertexShader", EffectBlockType.Pass, EffectStateValueType.Object),
        new("PixelShader", EffectBlockType.Pass, EffectStateValueType.Object),
        new("GeometryShader", EffectBlockType.Pass, EffectStateValueType.Object),
        new("DS_StencilRef", EffectBlockType.Pass, EffectStateValueType.UInt),
        new("AB_BlendFactor", EffectBlockType.Pass, EffectStateValueType.Float, 4),
        new("AB_SampleMask", EffectBlockType.Pass, EffectStateValueType.UInt),
        new("FillMode", EffectBlockType.Rasterizer, EffectStateValueType.UInt, 1, Fill),
        new("CullMode", EffectBlockType.Rasterizer, EffectStateValueType.UInt, 1, Cull),
        new("FrontCounterClockwise", EffectBlockType.Rasterizer, EffectStateValueType.Bool, 1, Bool),
        new("DepthBias", EffectBlockType.Rasterizer, EffectStateValueType.UInt),
        new("DepthBiasClamp", EffectBlockType.Rasterizer, EffectStateValueType.Float),
        new("SlopeScaledDepthBias", EffectBlockType.Rasterizer, EffectStateValueType.Float),
        new("DepthClipEnable", EffectBlockType.Rasterizer, EffectStateValueType.Bool, 1, Bool),
        new("ScissorEnable", EffectBlockType.Rasterizer, EffectStateValueType.Bool, 1, Bool),
        new("MultisampleEnable", EffectBlockType.Rasterizer, EffectStateValueType.Bool, 1, Bool),
        new("AntialiasedLineEnable", EffectBlockType.Rasterizer, EffectStateValueType.Bool, 1, Bool),
        new("DepthEnable", EffectBlockType.DepthStencil, EffectStateValueType.Bool, 1, Bool),
        new("DepthWriteMask", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, DepthWriteMask),
        new("DepthFunc", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, Comparison),
        new("StencilEnable", EffectBlockType.DepthStencil, EffectStateValueType.Bool, 1, Bool),
        new("StencilReadMask", EffectBlockType.DepthStencil, EffectStateValueType.UInt8),
        new("StencilWriteMask", EffectBlockType.DepthStencil, EffectStateValueType.UInt8),
        new("FrontFaceStencilFail", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("FrontFaceStencilDepthFail", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("FrontFaceStencilPass", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("FrontFaceStencilFunc", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, Comparison),
        new("BackFaceStencilFail", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("BackFaceStencilDepthFail", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("BackFaceStencilPass", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, StencilOp),
        new("BackFaceStencilFunc", EffectBlockType.DepthStencil, EffectStateValueType.UInt, 1, Comparison),
        new("AlphaToCoverageEnable", EffectBlockType.Blend, EffectStateValueType.Bool, 1, Bool),
        new("BlendEnable", EffectBlockType.Blend, EffectStateValueType.Bool, 1, Bool, 8),
        new("SrcBlend", EffectBlockType.Blend, EffectStateValueType.UInt, 1, Blend, 8, true),
        new("DestBlend", EffectBlockType.Blend, EffectStateValueType.UInt, 1, Blend, 8, true),
        new("BlendOp", EffectBlockType.Blend, EffectStateValueType.UInt, 1, BlendOp, 8, true),
        new("SrcBlendAlpha", EffectBlockType.Blend, EffectStateValueType.UInt, 1, Blend, 8, true),
        new("DestBlendAlpha", EffectBlockType.Blend, EffectStateValueType.UInt, 1, Blend, 8, true),
        new("BlendOpAlpha", EffectBlockType.Blend, EffectStateValueType.UInt, 1, BlendOp, 8, true),
        new("RenderTargetWriteMask", EffectBlockType.Blend, EffectStateValueType.UInt8, Indices: 8),
        new("Filter", EffectBlockType.Sampler, EffectStateValueType.UInt, 1, Filter),
        new("AddressU", EffectBlockType.Sampler, EffectStateValueType.UInt, 1, TextureAddress),
        new("AddressV", EffectBlockType.Sampler, EffectStateValueType.UInt, 1, TextureAddress),
        new("AddressW", EffectBlockType.Sampler, EffectStateValueType.UInt, 1, TextureAddress),
        new("MipLODBias", EffectBlockType.Sampler, EffectStateValueType.Float),
        new("MaxAnisotropy", EffectBlockType.Sampler, EffectStateValueType.UInt),
        new("ComparisonFunc", EffectBlockType.Sampler, EffectStateValueType.UInt, 1, Comparison),
        new("BorderColor", EffectBlockType.Sampler, EffectStateValueType.Float, 4),
        new("MinLOD", EffectBlockType.Sampler, EffectStateValueType.Float),
        new("MaxLOD", EffectBlockType.Sampler, EffectStateValueType.Float),
        new("Texture", EffectBlockType.Sampler, EffectStateValueType.Object),
        new("HullShader", EffectBlockType.Pass, EffectStateValueType.Object),
        new("DomainShader", EffectBlockType.Pass, EffectStateValueType.Object),
        new("ComputeShader", EffectBlockType.Pass, EffectStateValueType.Object),
    ];
}
