// An fx_5_0 effect holds shader model 4 shaders as readily as 5, a technique of each.
float4 tint;
float exposure;

float4 vs(float4 position : POSITION, inout float2 texcoord : TEXCOORD) : SV_Position
{
    return position * float4(1, 1, 1, exposure);
}

float4 ps(float4 position : SV_Position, float2 texcoord : TEXCOORD) : SV_Target
{
    return tint * exp2(texcoord.x * exposure);
}

technique10 Legacy
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, vs()));
        SetPixelShader(CompileShader(ps_4_0, ps()));
    }
}

technique11 Current
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, vs()));
        SetPixelShader(CompileShader(ps_5_0, ps()));
    }
}
