// Every stage a shader model 5 effect can set, in a group.
float4x4 viewProjection;
float tessellation;
float4 tint;
RWBuffer<float> scaled;

struct ControlPoint
{
    float4 position : SV_Position;
};

struct PatchConstants
{
    float edges[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

ControlPoint vs(float4 position : POSITION)
{
    ControlPoint o;
    o.position = position;
    return o;
}

PatchConstants hsConstants(InputPatch<ControlPoint, 3> patch)
{
    PatchConstants o;
    o.edges[0] = o.edges[1] = o.edges[2] = tessellation;
    o.inside = tessellation * 0.5;
    return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("hsConstants")]
ControlPoint hs(InputPatch<ControlPoint, 3> patch, uint id : SV_OutputControlPointID)
{
    return patch[id];
}

[domain("tri")]
ControlPoint ds(PatchConstants constants, float3 location : SV_DomainLocation,
    const OutputPatch<ControlPoint, 3> patch)
{
    ControlPoint o;
    float4 position = patch[0].position * location.x
        + patch[1].position * location.y
        + patch[2].position * location.z;
    o.position = mul(position, viewProjection);
    return o;
}

float4 ps(ControlPoint i) : SV_Target
{
    return tint * frac(i.position.x * 0.125);
}

[numthreads(64, 1, 1)]
void cs(uint3 id : SV_DispatchThreadID)
{
    scaled[id.x] = tint.x * id.x;
}

fxgroup Scene
{
    technique11 Tessellated
    {
        pass Draw
        {
            SetVertexShader(CompileShader(vs_5_0, vs()));
            SetHullShader(CompileShader(hs_5_0, hs()));
            SetDomainShader(CompileShader(ds_5_0, ds()));
            SetGeometryShader(NULL);
            SetPixelShader(CompileShader(ps_5_0, ps()));
        }
        pass Scale
        {
            SetComputeShader(CompileShader(cs_5_0, cs()));
        }
    }
}
