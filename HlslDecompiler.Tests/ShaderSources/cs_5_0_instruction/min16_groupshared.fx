StructuredBuffer<float4> src : register(t0);
RWStructuredBuffer<float4> dst : register(u0);

groupshared float4 g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	float4 r0;
	int4 r1;
	r0 = src[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex.x] = r0;
	GroupMemoryBarrierWithGroupSync();
	r1.x = i.sv_groupindex.x + 1;
	r1.x = r1.x & 63;
	r1 = asint(g0[r1.x]);
	r0 = r0 + asfloat(r1);
	dst[i.sv_dispatchthreadid.x] = r0;
}
