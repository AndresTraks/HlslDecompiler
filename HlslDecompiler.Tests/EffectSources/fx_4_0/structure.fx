// Everything an fx_4_0 effect holds besides its shaders: buffers named and not,
// a struct uniform, defaults, semantics and annotations on everything that takes
// them, strings, textures, every kind of state object, a shader variable shared
// by two passes, a stream output geometry shader declared and inline, an array of
// pixel shaders picked by a constant and by a uniform, and the calls that set
// blend and depth state with their extra arguments.
struct Light { float3 direction; float intensity; float4 color; };
cbuffer PerFrame : register(b1) <string Group = "frame";>
{
    float4x4 viewProjection : VIEWPROJECTION;
    row_major float3x4 bones[2];
    Light light = { float3(0, -1, 0), 2.0, float4(1, 1, 1, 1) };
};
float4 tint <string UIName = "Tint"; float UIMin = 0; int Steps[2] = {1, 2};> = float4(1, 0.5, 0.25, 1);
float scale = 2;
int mode = 3;
bool useFog = true;
string Description = "rich";
string Tags[2] = {"a", "b"};
Texture2D diffuse <string File = "diffuse.dds";>;
Texture2DArray layers;
TextureCube environment;
SamplerState linearSampler { Filter = MIN_MAG_MIP_LINEAR; AddressU = WRAP; AddressV = CLAMP; MaxAnisotropy = 4; BorderColor = float4(0, 0, 0, 1); MipLODBias = 0.5; };
SamplerComparisonState shadowSampler { Filter = COMPARISON_MIN_MAG_MIP_LINEAR; ComparisonFunc = LESS_EQUAL; };
BlendState additive { BlendEnable[0] = TRUE; SrcBlend = ONE; DestBlend = ONE; BlendOp = ADD; RenderTargetWriteMask[0] = 0x0F; AlphaToCoverageEnable = FALSE; };
BlendState noBlend { BlendEnable[0] = FALSE; };
DepthStencilState noDepth { DepthEnable = FALSE; DepthWriteMask = ZERO; DepthFunc = LESS; StencilEnable = TRUE; FrontFaceStencilPass = INCR; StencilReadMask = 0xF0; };
RasterizerState wire { FillMode = WIREFRAME; CullMode = NONE; DepthBias = 10; SlopeScaledDepthBias = 1.5; FrontCounterClockwise = TRUE; };
struct V { float4 p : SV_Position; float2 t : TEXCOORD0; };
V vs(float4 p : POSITION, float2 t : TEXCOORD0) { V o; o.p = mul(p, viewProjection) * scale; o.t = t; return o; }
[maxvertexcount(3)] void gs(triangle V i[3], inout TriangleStream<V> s) { for (int k = 0; k < 3; k++) s.Append(i[k]); }
float4 ps(V i) : SV_Target { return diffuse.Sample(linearSampler, i.t) * tint * light.color * (useFog ? 0.5 : 1); }
float4 ps2(V i) : SV_Target { return layers.Sample(linearSampler, float3(i.t, 1)) + environment.Sample(linearSampler, i.t.xyx) * mode; }
VertexShader sharedVs = CompileShader(vs_4_0, vs());
GeometryShader so = ConstructGSWithSO(CompileShader(gs_4_0, gs()), "SV_Position.xyzw; TEXCOORD0.xy");
PixelShader pss[2] = { CompileShader(ps_4_0, ps()), CompileShader(ps_4_0, ps2()) };
technique10 Main <string Mode = "main"; int Order = 1;>
{
    pass P0 <bool Enabled = true;>
    {
        SetVertexShader(sharedVs);
        SetGeometryShader(NULL);
        SetPixelShader(pss[1]);
        SetBlendState(additive, float4(0.5, 0.5, 0.5, 1), 0xFFFFFFFF);
        SetDepthStencilState(noDepth, 3);
        SetRasterizerState(wire);
    }
    pass P1
    {
        SetVertexShader(sharedVs);
        SetGeometryShader(so);
        SetPixelShader(CompileShader(ps_4_0, ps()));
        SetBlendState(noBlend, float4(0, 0, 0, 0), 0xFFFFFFFF);
    }
}
technique10 Second
{
    pass P0
    {
        SetVertexShader(CompileShader(vs_4_0, vs()));
        SetGeometryShader(ConstructGSWithSO(CompileShader(gs_4_0, gs()), "SV_Position.xyzw"));
        SetPixelShader(pss[mode]);
    }
}
