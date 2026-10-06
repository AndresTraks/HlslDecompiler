// A shader model 4.1 instruction from inside an effect.
Texture2D shadowMap;
SamplerState pointSampler
{
    Filter = MIN_MAG_MIP_POINT;
    AddressU = Clamp;
    AddressV = Clamp;
};
float depthBias;

float4 vs(float4 position : POSITION, inout float3 texcoord : TEXCOORD) : SV_Position
{
    return position;
}

float4 ps(float4 position : SV_Position, float3 texcoord : TEXCOORD) : SV_Target
{
    float4 depths = shadowMap.Gather(pointSampler, texcoord.xy);
    float4 lit = step(texcoord.z - depthBias, depths);
    return dot(lit, 0.25);
}

technique10 Shadow
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_1, vs()));
        SetGeometryShader(NULL);
        SetPixelShader(CompileShader(ps_4_1, ps()));
    }
}
