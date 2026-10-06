// A vertex shader set by name, pixel shaders chosen from an array by an
// expression the effect evaluates, a shader model 1 vertex shader, and sampler
// state - none of which are shaders, and all of which sit in the effect beside
// the ones that are.
string Note <string Hint = "tint"; > = "ps_2_0";

float4x4 worldViewProjection;
float4 tint;
float scale;
int variant;

texture diffuse;
sampler diffuseSampler = sampler_state
{
    Texture = <diffuse>;
    MinFilter = Linear;
    MagFilter = Linear;
};

float4 vs(float4 position : POSITION, inout float2 texcoord : TEXCOORD0) : POSITION
{
    return mul(position, worldViewProjection);
}

float4 vs_scaled(float4 position : POSITION, inout float2 texcoord : TEXCOORD0) : POSITION
{
    return position * tint;
}

float4 ps(float2 texcoord : TEXCOORD0) : COLOR
{
    return tex2D(diffuseSampler, texcoord) * tint * (scale * 2 + 1);
}

float4 ps_plain(float2 texcoord : TEXCOORD0) : COLOR
{
    return tex2D(diffuseSampler, texcoord) * tint;
}

VertexShader transform = compile vs_2_0 vs();
PixelShader variants[2] = { compile ps_2_0 ps(), compile ps_2_0 ps_plain() };

technique Draw
{
    pass Chosen
    {
        VertexShader = (transform);
        PixelShader = (variants[variant]);
        ZEnable = true;
    }
    pass Scaled
    {
        VertexShader = compile vs_1_1 vs_scaled();
        PixelShader = compile ps_2_0 ps_plain();
    }
}
