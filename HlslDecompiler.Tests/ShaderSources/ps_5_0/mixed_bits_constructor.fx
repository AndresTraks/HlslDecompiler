SamplerComparisonState cmp;
Texture2D<uint2> stencil;
Texture2D<float> depth;

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 uv : UV;
};

float4 main(PS_IN i) : SV_Target
{
	int3 t0 = int3((int2)i.sv_position.xy, 0);
	return float4(depth.SampleCmpLevelZero(cmp, i.uv, asfloat(stencil.Load(t0).y)).x, (float)(stencil.Load(t0).x & 255), asfloat(stencil.Load(t0).x >> 8), 1);
}
