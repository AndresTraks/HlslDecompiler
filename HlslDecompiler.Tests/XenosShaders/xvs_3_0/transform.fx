float4x4 wvp : register(c0);
float4 tint : register(c4);
struct VOut { float4 pos : POSITION; float4 col : COLOR0; float2 uv : TEXCOORD0; };
VOut main(float4 pos : POSITION, float2 uv : TEXCOORD0)
{
    VOut o;
    o.pos = mul(pos, wvp);
    o.col = tint * 2;
    o.uv = uv;
    return o;
}
