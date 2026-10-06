using System.Collections.Generic;
using System.Linq;

namespace HlslDecompiler.DirectXShaderModel;

/// <summary>
/// A state an fx_2_0 effect sets: its name in an effect file, whether it is one
/// of several told apart by an index - a texture stage, a sampler, a light, a
/// transform, a constant register - and the names its values have.
/// </summary>
public sealed record D3D9EffectStateInfo(string Name, bool IsIndexed = false, IReadOnlyDictionary<uint, string> ValueNames = null);

/// <summary>
/// D3DX9's table of the states an effect can set, which a state indexes - in the
/// order of state_table in Wine's d3dx9_36/effect.c. The value names are the
/// Direct3D 9 enumerations' without their prefixes, which is how an effect file
/// writes them.
/// </summary>
public static class D3D9EffectStates
{
    private static Dictionary<uint, string> Names(params string[] names)
    {
        return names.Select((name, i) => (name, i)).Where(n => n.name != null)
            .ToDictionary(n => (uint)n.i, n => n.name);
    }

    private static readonly Dictionary<uint, string> Bool = new() { [0] = "FALSE", [1] = "TRUE" };
    private static readonly Dictionary<uint, string> Fill = Names(null, "Point", "Wireframe", "Solid");
    private static readonly Dictionary<uint, string> Shade = Names(null, "Flat", "Gouraud", "Phong");
    private static readonly Dictionary<uint, string> Blend = Names(null, "Zero", "One", "SrcColor", "InvSrcColor",
        "SrcAlpha", "InvSrcAlpha", "DestAlpha", "InvDestAlpha", "DestColor", "InvDestColor", "SrcAlphaSat",
        "BothSrcAlpha", "BothInvSrcAlpha", "BlendFactor", "InvBlendFactor");
    private static readonly Dictionary<uint, string> Cull = Names(null, "None", "CW", "CCW");
    private static readonly Dictionary<uint, string> Comparison = Names(null, "Never", "Less", "Equal", "LessEqual",
        "Greater", "NotEqual", "GreaterEqual", "Always");
    private static readonly Dictionary<uint, string> StencilOp = Names(null, "Keep", "Zero", "Replace", "IncrSat",
        "DecrSat", "Invert", "Incr", "Decr");
    private static readonly Dictionary<uint, string> BlendOp = Names(null, "Add", "Subtract", "RevSubtract", "Min", "Max");
    private static readonly Dictionary<uint, string> Fog = Names("None", "Exp", "Exp2", "Linear");
    private static readonly Dictionary<uint, string> TextureFilter = Names("None", "Point", "Linear", "Anisotropic",
        null, null, "PyramidalQuad", "GaussianQuad");
    private static readonly Dictionary<uint, string> TextureAddress = Names(null, "Wrap", "Mirror", "Clamp", "Border", "MirrorOnce");
    private static readonly Dictionary<uint, string> MaterialSource = Names("Material", "Color1", "Color2");
    private static readonly Dictionary<uint, string> TextureOp = Names(null, "Disable", "SelectArg1", "SelectArg2",
        "Modulate", "Modulate2x", "Modulate4x", "Add", "AddSigned", "AddSigned2x", "Subtract", "AddSmooth",
        "BlendDiffuseAlpha", "BlendTextureAlpha", "BlendFactorAlpha", "BlendTextureAlphaPM", "BlendCurrentAlpha",
        "PreModulate", "ModulateAlpha_AddColor", "ModulateColor_AddAlpha", "ModulateInvAlpha_AddColor",
        "ModulateInvColor_AddAlpha", "BumpEnvMap", "BumpEnvMapLuminance", "DotProduct3", "MultiplyAdd", "Lerp");
    private static readonly Dictionary<uint, string> TextureArgument = Names("Diffuse", "Current", "Texture",
        "TFactor", "Specular", "Temp", "Constant");

    public static readonly IReadOnlyList<D3D9EffectStateInfo> All = Build();

    private static List<D3D9EffectStateInfo> Build()
    {
        var states = new List<D3D9EffectStateInfo>
        {
            new("ZEnable", ValueNames: Bool),
            new("FillMode", ValueNames: Fill),
            new("ShadeMode", ValueNames: Shade),
            new("ZWriteEnable", ValueNames: Bool),
            new("AlphaTestEnable", ValueNames: Bool),
            new("LastPixel", ValueNames: Bool),
            new("SrcBlend", ValueNames: Blend),
            new("DestBlend", ValueNames: Blend),
            new("CullMode", ValueNames: Cull),
            new("ZFunc", ValueNames: Comparison),
            new("AlphaRef"),
            new("AlphaFunc", ValueNames: Comparison),
            new("DitherEnable", ValueNames: Bool),
            new("AlphaBlendEnable", ValueNames: Bool),
            new("FogEnable", ValueNames: Bool),
            new("SpecularEnable", ValueNames: Bool),
            new("FogColor"),
            new("FogTableMode", ValueNames: Fog),
            new("FogStart"),
            new("FogEnd"),
            new("FogDensity"),
            new("RangeFogEnable", ValueNames: Bool),
            new("StencilEnable", ValueNames: Bool),
            new("StencilFail", ValueNames: StencilOp),
            new("StencilZFail", ValueNames: StencilOp),
            new("StencilPass", ValueNames: StencilOp),
            new("StencilFunc", ValueNames: Comparison),
            new("StencilRef"),
            new("StencilMask"),
            new("StencilWriteMask"),
            new("TextureFactor"),
        };
        for (int i = 0; i < 16; i++)
        {
            states.Add(new($"Wrap{i}"));
        }
        states.AddRange(
        [
            new("Clipping", ValueNames: Bool),
            new("Lighting", ValueNames: Bool),
            new("Ambient"),
            new("FogVertexMode", ValueNames: Fog),
            new("ColorVertex", ValueNames: Bool),
            new("LocalViewer", ValueNames: Bool),
            new("NormalizeNormals", ValueNames: Bool),
            new("DiffuseMaterialSource", ValueNames: MaterialSource),
            new("SpecularMaterialSource", ValueNames: MaterialSource),
            new("AmbientMaterialSource", ValueNames: MaterialSource),
            new("EmissiveMaterialSource", ValueNames: MaterialSource),
            new("VertexBlend"),
            new("ClipPlaneEnable"),
            new("PointSize"),
            new("PointSize_Min"),
            new("PointSize_Max"),
            new("PointSpriteEnable", ValueNames: Bool),
            new("PointScaleEnable", ValueNames: Bool),
            new("PointScale_A"),
            new("PointScale_B"),
            new("PointScale_C"),
            new("MultiSampleAntialias", ValueNames: Bool),
            new("MultiSampleMask"),
            new("PatchEdgeStyle"),
            new("DebugMonitorToken"),
            new("IndexedVertexBlendEnable", ValueNames: Bool),
            new("ColorWriteEnable"),
            new("TweenFactor"),
            new("BlendOp", ValueNames: BlendOp),
            new("PositionDegree"),
            new("NormalDegree"),
            new("ScissorTestEnable", ValueNames: Bool),
            new("SlopeScaleDepthBias"),
            new("AntialiasedLineEnable", ValueNames: Bool),
            new("MinTessellationLevel"),
            new("MaxTessellationLevel"),
            new("AdaptiveTess_X"),
            new("AdaptiveTess_Y"),
            new("AdaptiveTess_Z"),
            new("AdaptiveTess_W"),
            new("EnableAdaptiveTessellation", ValueNames: Bool),
            new("TwoSidedStencilMode", ValueNames: Bool),
            new("CCW_StencilFail", ValueNames: StencilOp),
            new("CCW_StencilZFail", ValueNames: StencilOp),
            new("CCW_StencilPass", ValueNames: StencilOp),
            new("CCW_StencilFunc", ValueNames: Comparison),
            new("ColorWriteEnable1"),
            new("ColorWriteEnable2"),
            new("ColorWriteEnable3"),
            new("BlendFactor"),
            new("SRGBWriteEnable", ValueNames: Bool),
            new("DepthBias"),
            new("SeparateAlphaBlendEnable", ValueNames: Bool),
            new("SrcBlendAlpha", ValueNames: Blend),
            new("DestBlendAlpha", ValueNames: Blend),
            new("BlendOpAlpha", ValueNames: BlendOp),
            // Texture stages.
            new("ColorOp", true, TextureOp),
            new("ColorArg0", true, TextureArgument),
            new("ColorArg1", true, TextureArgument),
            new("ColorArg2", true, TextureArgument),
            new("AlphaOp", true, TextureOp),
            new("AlphaArg0", true, TextureArgument),
            new("AlphaArg1", true, TextureArgument),
            new("AlphaArg2", true, TextureArgument),
            new("ResultArg", true, TextureArgument),
            new("BumpEnvMat00", true),
            new("BumpEnvMat01", true),
            new("BumpEnvMat10", true),
            new("BumpEnvMat11", true),
            new("TexCoordIndex", true),
            new("BumpEnvLScale", true),
            new("BumpEnvLOffset", true),
            new("TextureTransformFlags", true),
            new("Constant", true),
            new("NPatchMode"),
            new("FVF"),
            new("ProjectionTransform"),
            new("ViewTransform"),
            new("WorldTransform", true),
            new("TextureTransform", true),
            new("MaterialDiffuse"),
            new("MaterialAmbient"),
            new("MaterialSpecular"),
            new("MaterialEmissive"),
            new("MaterialPower"),
            new("LightType", true),
            new("LightDiffuse", true),
            new("LightSpecular", true),
            new("LightAmbient", true),
            new("LightPosition", true),
            new("LightDirection", true),
            new("LightRange", true),
            new("LightFallOff", true),
            new("LightAttenuation0", true),
            new("LightAttenuation1", true),
            new("LightAttenuation2", true),
            new("LightTheta", true),
            new("LightPhi", true),
            new("LightEnable", true, Bool),
            new("VertexShader"),
            new("PixelShader"),
            new("VertexShaderConstantF", true),
            new("VertexShaderConstantB", true),
            new("VertexShaderConstantI", true),
            new("VertexShaderConstant", true),
            new("VertexShaderConstant1", true),
            new("VertexShaderConstant2", true),
            new("VertexShaderConstant3", true),
            new("VertexShaderConstant4", true),
            new("PixelShaderConstantF", true),
            new("PixelShaderConstantB", true),
            new("PixelShaderConstantI", true),
            new("PixelShaderConstant", true),
            new("PixelShaderConstant1", true),
            new("PixelShaderConstant2", true),
            new("PixelShaderConstant3", true),
            new("PixelShaderConstant4", true),
            new("Texture", true),
            // Sampler states: indexed by the sampler in a pass, not in a sampler.
            new("AddressU", true, TextureAddress),
            new("AddressV", true, TextureAddress),
            new("AddressW", true, TextureAddress),
            new("BorderColor", true),
            new("MagFilter", true, TextureFilter),
            new("MinFilter", true, TextureFilter),
            new("MipFilter", true, TextureFilter),
            new("MipMapLodBias", true),
            new("MaxMipLevel", true),
            new("MaxAnisotropy", true),
            new("SRGBTexture", true, Bool),
            new("ElementIndex", true),
            new("DMAPOffset", true),
            new("Sampler", true),
        ]);
        return states;
    }
}
