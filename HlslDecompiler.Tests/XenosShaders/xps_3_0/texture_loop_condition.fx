sampler2D s : register(s0);
float4 k[8] : register(c0);
int n : register(i0);
float4 main(float2 uv : TEXCOORD0) : COLOR
{
    float4 acc = 0;
    for (int i = 0; i < n; i++)
        acc += tex2D(s, uv + k[i].xy) * k[i].z;
    if (acc.w > 0.5) acc.xyz = sqrt(acc.xyz);
    return acc;
}
