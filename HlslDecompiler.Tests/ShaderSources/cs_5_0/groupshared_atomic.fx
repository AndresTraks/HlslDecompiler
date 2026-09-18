RWStructuredBuffer<uint> flags : register(u0);

groupshared int g0[64];

struct CS_IN
{
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	g0[i.sv_groupthreadid.x] = 0;
	GroupMemoryBarrierWithGroupSync();
	InterlockedOr(g0[i.sv_groupthreadid.x & 31], 1 << (i.sv_groupthreadid.x & 15));
	InterlockedAnd(g0[i.sv_groupthreadid.x & 7], 65535);
	InterlockedCompareStore(g0[i.sv_groupthreadid.x & 3], 0, i.sv_groupid.x);
	GroupMemoryBarrierWithGroupSync();
	flags[i.sv_groupthreadid.x] = g0[i.sv_groupthreadid.x];
}
