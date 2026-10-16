cbuffer C : register(b0)
{
	float4 f[8];
	int4 n[8];
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
	return f[t0] * (float4)(n[t0].x) + asfloat(n[t0 ^ 1]) + (float4)(n[t0].y);
}
