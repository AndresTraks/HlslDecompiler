sampler2D s : register(s0);
float4 tint : register(c0);
float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float4 c = tex2D(s, uv);
    return c * tint + float4(0.5, 0.25, 0, 0) * c.w;
}
