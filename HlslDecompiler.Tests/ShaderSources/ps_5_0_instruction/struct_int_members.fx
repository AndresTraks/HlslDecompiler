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
	float4 o;

	int3 r0;
	int2 r1;
	r0.xy = int2(b[i.idx].u.w, b[i.idx].s);
	r0.z = (0 < r0.y) ? -1 : 0;
	o.w = (r0.z != 0) ? asfloat(r0.x) : i.texcoord.w;
	o.z = (float)r0.y;
	r0.x = i.idx & 1;
	r0.yz = asint(b[i.idx].f.xy);
	r1 = b[i.idx].u.xy;
	o.x = (r0.x != 0) ? asfloat(r1.x) : asfloat(r0.y);
	o.y = asfloat(r0.z ^ r1.y);

	return o;
}
