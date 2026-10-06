// Everything an fx_2_0 effect holds besides its shaders: parameters of every
// class with defaults, semantics and annotations, a struct, a shared parameter,
// strings, textures and samplers with their states, shader parameters set by
// name and an array of them picked by a parameter, render states set by name, by
// number and by an expression, a texture and a sampler set in a pass, and a
// fixed function pass with texture stage states and a transform.
struct Light { float3 direction; float intensity; };
float4x4 worldViewProjection : WORLDVIEWPROJECTION <string UIWidget = "None";>;
row_major float4x3 bones[2];
float4 tint <string UIName = "Tint"; float UIMin = 0; int Steps[2] = {1, 2};> = float4(1, 0.5, 0.25, 1);
float alphaCut = 0.5;
float scale = 2;
int variant = 1;
bool useFog = true;
Light light = { float3(0, -1, 0), 2 };
float weights[3] = { 0.25, 0.5, 0.25 };
string Description = "rich d3d9";
shared float4 sharedColor;
texture diffuseTexture <string ResourceName = "diffuse.dds";>;
texture cubeTexture;
sampler2D diffuseSampler = sampler_state
{
    Texture = <diffuseTexture>;
    MinFilter = Linear; MagFilter = Linear; MipFilter = Point;
    AddressU = Wrap; AddressV = Clamp;
    BorderColor = 0xFF00FF00;
    MaxAnisotropy = 4;
};
samplerCUBE cubeSampler = sampler_state { Texture = <cubeTexture>; };
struct V { float4 p : POSITION; float2 t : TEXCOORD0; float3 n : TEXCOORD1; };
V vs(float4 p : POSITION, float2 t : TEXCOORD0, float3 n : NORMAL) { V o; o.p = mul(p, worldViewProjection); o.t = t; o.n = n * light.intensity; return o; }
float4 ps(V i) : COLOR { return tex2D(diffuseSampler, i.t) * tint * (useFog ? 0.5 : 1); }
float4 ps_env(V i) : COLOR { return texCUBE(cubeSampler, i.n) * scale + sharedColor; }
float4 ps_weights(V i) : COLOR { return tex2D(diffuseSampler, i.t) * weights[0] + tex2D(diffuseSampler, i.t + 0.01) * weights[1]; }
VertexShader transform = compile vs_2_0 vs();
PixelShader variants[2] = { compile ps_2_0 ps(), compile ps_2_0 ps_env() };
technique Main <string Mode = "main"; int Order = 1;>
{
    pass Opaque <bool Enabled = true;>
    {
        VertexShader = (transform);
        PixelShader = (variants[variant]);
        ZEnable = true;
        ZWriteEnable = true;
        ZFunc = LessEqual;
        CullMode = CCW;
        AlphaBlendEnable = false;
        ColorWriteEnable = Red | Green | Blue;
    }
    pass Blended
    {
        VertexShader = compile vs_2_0 vs();
        PixelShader = compile ps_2_0 ps_weights();
        AlphaBlendEnable = true;
        SrcBlend = SrcAlpha;
        DestBlend = InvSrcAlpha;
        BlendOp = Add;
        AlphaTestEnable = true;
        AlphaRef = (alphaCut * 255);
        AlphaFunc = Greater;
        FogColor = 0x00808080;
        PointSize = (scale * 2);
        Texture[0] = <diffuseTexture>;
        Sampler[1] = (diffuseSampler);
        StencilEnable = true;
        StencilFunc = Always;
        StencilPass = Replace;
        StencilRef = 3;
    }
}
technique Fixed
{
    pass P0
    {
        VertexShader = NULL;
        PixelShader = NULL;
        Lighting = false;
        ColorOp[0] = Modulate;
        ColorArg1[0] = Texture;
        ColorArg2[0] = Diffuse;
        AlphaOp[0] = SelectArg1;
        MinFilter[0] = Linear;
        WorldTransform[0] = (worldViewProjection);
        TextureFactor = 0xFFFFFFFF;
    }
}
