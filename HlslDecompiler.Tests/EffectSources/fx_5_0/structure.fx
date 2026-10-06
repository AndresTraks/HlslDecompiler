// What fx_5_0 adds: groups and a technique outside of any, shader model 5
// stages, a geometry shader streaming to four streams with one rasterized,
// blend state set differently per render target, unordered access views, and a
// uniform placed where it was told to go.
cbuffer Placed
{
    float4 first : packoffset(c1);
    float second : packoffset(c0.y);
};
float4 tint = float4(1, 1, 1, 1);
float tessellation <float UIMax = 64;> = 4;
RWTexture2D<float4> output;
RWStructuredBuffer<float> results;
Texture2D<uint4> indices;

BlendState perTarget
{
    BlendEnable[0] = TRUE;
    BlendEnable[1] = FALSE;
    SrcBlend[0] = SRC_ALPHA;
    SrcBlend[1] = ONE;
    DestBlend = INV_SRC_ALPHA;
    RenderTargetWriteMask[1] = 0x07;
};

struct Vertex
{
    float4 position : SV_Position;
    float2 texcoord : TEXCOORD0;
};

struct Patch
{
    float edges[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

Vertex vs(float4 position : POSITION, float2 texcoord : TEXCOORD0)
{
    Vertex o;
    o.position = position * tint;
    o.texcoord = texcoord;
    return o;
}

Patch patchConstants(InputPatch<Vertex, 3> patch)
{
    Patch o;
    o.edges[0] = o.edges[1] = o.edges[2] = tessellation;
    o.inside = tessellation;
    return o;
}

[domain("tri")]
[partitioning("fractional_odd")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("patchConstants")]
Vertex hs(InputPatch<Vertex, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

[domain("tri")]
Vertex ds(Patch constants, float3 location : SV_DomainLocation, const OutputPatch<Vertex, 3> patch)
{
    Vertex o;
    o.position = patch[0].position * location.x + patch[1].position * location.y + patch[2].position * location.z;
    o.texcoord = patch[0].texcoord * location.x + patch[1].texcoord * location.y + patch[2].texcoord * location.z;
    return o;
}

struct Streamed
{
    float4 position : SV_Position;
};

[maxvertexcount(3)]
void gs(triangle Vertex input[3], inout PointStream<Streamed> first, inout PointStream<Streamed> second)
{
    for (int i = 0; i < 3; i++)
    {
        Streamed o;
        o.position = input[i].position;
        first.Append(o);
        o.position = input[i].position * 2;
        second.Append(o);
    }
}

float4 ps(Vertex i) : SV_Target
{
    return float4(i.texcoord, 0, 1) * tint + float4(indices.Load(int3(i.position.xy, 0)).xy, 0, 0);
}

[numthreads(8, 8, 1)]
void cs(uint3 id : SV_DispatchThreadID)
{
    output[id.xy] = tint * id.x;
    results[id.x] = first.x * second;
}

GeometryShader streams = ConstructGSWithSO(CompileShader(gs_5_0, gs()), "0:SV_Position.xyzw", "1:SV_Position.xy", NULL, NULL, 1);

technique11 Plain
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, vs()));
        SetGeometryShader(NULL);
        SetPixelShader(CompileShader(ps_5_0, ps()));
        SetBlendState(perTarget, float4(1, 1, 1, 1), 0xFFFFFFFF);
    }
}

fxgroup Tessellated <string Purpose = "terrain";>
{
    technique11 Draw
    {
        pass P0
        {
            SetVertexShader(CompileShader(vs_5_0, vs()));
            SetHullShader(CompileShader(hs_5_0, hs()));
            SetDomainShader(CompileShader(ds_5_0, ds()));
            SetGeometryShader(streams);
            SetPixelShader(NULL);
        }
    }
    technique11 Compute
    {
        pass P0
        {
            SetComputeShader(CompileShader(cs_5_0, cs()));
        }
    }
}
