// Shader model 3 from inside an effect: a loop, a branch on a uniform, and a
// scale worked out from uniforms alone, which a preshader does.
float4x4 worldViewProjection;
float4 tint;
float blur;
float exposure;
bool flip;

sampler diffuseSampler;

float4 vs(float4 position : POSITION, inout float2 texcoord : TEXCOORD0) : POSITION
{
    if (flip)
    {
        texcoord.y = 1 - texcoord.y;
    }
    return mul(position, worldViewProjection);
}

float4 ps(float2 texcoord : TEXCOORD0) : COLOR
{
    float4 sum = 0;
    for (int i = 0; i < 4; i++)
    {
        sum += tex2D(diffuseSampler, texcoord + float2(i * blur, 0));
    }
    return sum * 0.25 * tint * exp2(exposure);
}

technique Blur
{
    pass P0
    {
        VertexShader = compile vs_3_0 vs();
        PixelShader = compile ps_3_0 ps();
    }
}
