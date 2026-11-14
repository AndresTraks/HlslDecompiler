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
	float4 t0 = src[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	float4 t1 = g0[i.sv_groupindex + 1 & 63];
	dst[i.sv_dispatchthreadid.x] = t0 + t1;
}
