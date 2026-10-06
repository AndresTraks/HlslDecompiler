// Stages that read only some of what the stage before them writes: the geometry,
// hull and domain shaders here read position and color and skip the texcoord
// between them and the normal after. In a pass the registers have to line up,
// whatever is read, so each declares its whole input.
float4 tint;

struct V
{
    float4 position : SV_Position;
    float2 texcoord : TEXCOORD0;
    float4 color : COLOR0;
    float3 normal : NORMAL;
};

struct P
{
    float edges[3] : SV_TessFactor;
    float inside : SV_InsideTessFactor;
};

V vs(float4 p : POSITION, float2 t : TEXCOORD0, float4 c : COLOR0, float3 n : NORMAL)
{
    V o;
    o.position = p;
    o.texcoord = t;
    o.color = c;
    o.normal = n;
    return o;
}

[maxvertexcount(3)]
void gs(triangle V i[3], inout TriangleStream<V> s)
{
    for (int k = 0; k < 3; k++)
    {
        V o = (V)0;
        o.position = i[k].position;
        o.color = i[k].color * tint;
        s.Append(o);
    }
}

P pc(InputPatch<V, 3> patch)
{
    P o;
    o.edges[0] = o.edges[1] = o.edges[2] = tint.x;
    o.inside = tint.y;
    return o;
}

[domain("tri")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(3)]
[patchconstantfunc("pc")]
V hs(InputPatch<V, 3> patch, uint id : SV_OutputControlPointID)
{
    V o = (V)0;
    o.position = patch[id].position;
    o.color = patch[id].color;
    return o;
}

[domain("tri")]
V ds(P constants, float3 b : SV_DomainLocation, const OutputPatch<V, 3> patch)
{
    V o = (V)0;
    o.position = patch[0].position * b.x + patch[1].position * b.y + patch[2].position * b.z;
    o.color = patch[0].color;
    return o;
}

float4 ps(V i) : SV_Target
{
    return i.color;
}

technique11 Geometry
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, vs()));
        SetGeometryShader(CompileShader(gs_5_0, gs()));
        SetPixelShader(CompileShader(ps_5_0, ps()));
    }
}

technique11 Tessellation
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_5_0, vs()));
        SetHullShader(CompileShader(hs_5_0, hs()));
        SetDomainShader(CompileShader(ds_5_0, ds()));
        SetPixelShader(CompileShader(ps_5_0, ps()));
    }
}
