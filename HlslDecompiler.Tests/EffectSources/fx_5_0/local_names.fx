// Two shaders of a kind that each have names nothing in the bytecode gives them:
// literal arrays read at an index, and groupshared memory. Decompiled on their
// own, both pixel shaders' arrays are icb and both compute shaders' memory is
// g0; in one effect they are functions side by side and need names apart.
int index;
RWBuffer<float> results;

float4 ps_a(float4 p : SV_Position) : SV_Target
{
    static const float4 table[3] = { float4(1, 0, 0, 1), float4(0, 1, 0, 1), float4(0, 0, 1, 1) };
    return table[index];
}

float4 ps_b(float4 p : SV_Position) : SV_Target
{
    static const float4 table[2] = { float4(0.5, 0, 0, 1), float4(0, 0.5, 0, 1) };
    return table[index];
}

groupshared float shared_a[64];

[numthreads(64, 1, 1)]
void cs_a(uint3 id : SV_DispatchThreadID, uint g : SV_GroupIndex)
{
    shared_a[g] = id.x;
    GroupMemoryBarrierWithGroupSync();
    results[id.x] = shared_a[63 - g];
}

groupshared float shared_b[32];

[numthreads(32, 1, 1)]
void cs_b(uint3 id : SV_DispatchThreadID, uint g : SV_GroupIndex)
{
    shared_b[g] = id.x * 2;
    GroupMemoryBarrierWithGroupSync();
    results[id.x] = shared_b[31 - g];
}

technique11 Tables
{
    pass P0
    {
        SetPixelShader(CompileShader(ps_5_0, ps_a()));
    }
    pass P1
    {
        SetPixelShader(CompileShader(ps_5_0, ps_b()));
    }
}

technique11 Compute
{
    pass P0
    {
        SetComputeShader(CompileShader(cs_5_0, cs_a()));
    }
    pass P1
    {
        SetComputeShader(CompileShader(cs_5_0, cs_b()));
    }
}
