// A geometry shader that reads only some of what the vertex shader writes, and
// a pixel shader that reads only some of what the geometry shader writes. In a
// pass the registers have to line up, whatever is read, so each declares its
// whole input.
float4 tint;

struct V
{
    float4 position : SV_Position;
    float2 texcoord : TEXCOORD0;
    float4 color : COLOR0;
    float3 normal : NORMAL;
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
        V o = i[k];
        o.color = i[k].color * tint;
        o.texcoord = 0;
        s.Append(o);
    }
}

float4 ps(V i) : SV_Target
{
    return float4(i.normal, 1) * i.color.a;
}

technique10 Geometry
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, vs()));
        SetGeometryShader(CompileShader(gs_4_0, gs()));
        SetPixelShader(CompileShader(ps_4_0, ps()));
    }
}
