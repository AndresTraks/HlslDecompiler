cbuffer C : register(b0)
{
	float4 f[8];
	int4 n[8];
	uint4 u[8];
	uint k;
};

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = i.idx + k & 7;
	return asfloat(u[t0]) + float4(i.idx & 1 ? asfloat(n[t0].x) : i.texcoord.x, asfloat(asint(i.texcoord.y) ^ u[t0].y), (float)(u[t0].z), n[t0].w < 0 ? asfloat(u[t0 ^ 3].w) : f[t0].w);
}
