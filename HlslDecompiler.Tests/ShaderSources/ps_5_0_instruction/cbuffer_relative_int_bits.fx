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
	float4 o;

	int4 r0;
	float4 r1;
	r0.x = i.idx + k;
	r0.x = r0.x & 7;
	r0.y = r0.x ^ 1;
	r0.zw = asint((float2)n[r0.x].xy);
	r1 = f[r0.x] * asfloat(r0.z) + asfloat(n[r0.y]);
	o = asfloat(r0.w) + r1;

	return o;
}
