struct E
{
	float4 f;
	uint4 u;
	int s;
};

StructuredBuffer<E> b : register(t0);

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float2 t0 = b[i.idx].f.xy;
	uint2 t1 = b[i.idx].u.xy;
	int2 t2 = int2(b[i.idx].u.w, b[i.idx].s);
	return float4(i.idx & 1 ? asfloat(t1.x) : t0.x, asfloat(asint(t0.y) ^ t1.y), (float)t2.y, t2.y > 0 ? asfloat(t2.x) : i.texcoord.w);
}
