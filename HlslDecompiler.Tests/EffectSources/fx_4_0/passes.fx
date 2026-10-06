// One vertex shader set by name from two passes, a pass with no geometry
// shader and one whose geometry shader streams out, and strings that read
// DXBC sitting in the effect beside the shaders.
string Note <string Hint = "DXBC"; > = "DXBC";

float4x4 worldViewProjection;
float4 tint;
float extrude;

struct VertexOutput
{
    float4 position : SV_Position;
    float3 normal : NORMAL;
};

VertexOutput vs(float4 position : POSITION, float3 normal : NORMAL)
{
    VertexOutput o;
    o.position = mul(position, worldViewProjection);
    o.normal = normal;
    return o;
}

[maxvertexcount(3)]
void gs(triangle VertexOutput input[3], inout TriangleStream<VertexOutput> stream)
{
    for (int i = 0; i < 3; i++)
    {
        VertexOutput o = input[i];
        o.position.xyz += o.normal * extrude;
        stream.Append(o);
    }
}

float4 ps(VertexOutput i) : SV_Target
{
    return tint * saturate(dot(normalize(i.normal), float3(0, 0, 1)));
}

float4 ps_flat(VertexOutput i) : SV_Target
{
    return tint;
}

VertexShader transform = CompileShader(vs_4_0, vs());
GeometryShader extrudeOut = ConstructGSWithSO(CompileShader(gs_4_0, gs()), "SV_Position.xyzw; NORMAL.xyz");

technique10 Draw
{
    pass Lit
    {
        SetVertexShader(transform);
        SetGeometryShader(NULL);
        SetPixelShader(CompileShader(ps_4_0, ps()));
    }
    pass Extruded
    {
        SetVertexShader(transform);
        SetGeometryShader(extrudeOut);
        SetPixelShader(CompileShader(ps_4_0, ps_flat()));
    }
}
