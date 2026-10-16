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
	float4 o;

	int3 r0;
	int4 r1;
	r0.x = i.idx + k;
	r0.x = r0.x & 7;
	r0.y = (n[r0.x].w < 0) ? -1 : 0;
	r0.z = r0.x ^ 3;
	r1.w = (r0.y != 0) ? u[r0.z].w : asint(f[r0.x].w);
	r0.y = i.idx & 1;
	r1.x = (r0.y != 0) ? n[r0.x].x : asint(i.texcoord.x);
	r1.y = asint(i.texcoord.y) ^ u[r0.x].y;
	r1.z = asint((float)(uint)u[r0.x].z);
	o = asfloat(r1) + asfloat(u[r0.x]);

	return o;
}
