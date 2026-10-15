StructuredBuffer<float4> a : register(t0);
RWStructuredBuffer<float4> o : register(u0);

struct G0Element
{
	float4 m0;
	float m4;
};

groupshared G0Element g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int4 r0;
	float4 r1;
	r0 = asint(a[i.sv_dispatchthreadid.x]);
	r0.w = r0.w ^ i.sv_groupindex.x;
	g0[i.sv_groupindex.x].m0 = asfloat(r0);
	r0.x = asint((float)(uint)i.sv_groupindex.x);
	g0[i.sv_groupindex.x].m4 = asfloat(r0.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = i.sv_groupindex.x + 1;
	r0.x = r0.x & 63;
	r0.yzw = asint(g0[r0.x].m0.xyz);
	r1.xw = float2(g0[r0.x].m4, g0[r0.x].m0.w);
	r1.xyz = asfloat(r0.yzw) * r1.xxx;
	o[i.sv_dispatchthreadid.x] = r1;
}
