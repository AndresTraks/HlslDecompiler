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
	int4 r0;
	int4 r1;
	g0[i.sv_groupthreadid.x] = 0;
	GroupMemoryBarrierWithGroupSync();
	r0 = i.sv_groupthreadid.x & int4(3, 31, 15, 7);
	r0.z = 1 << r0.z;
	r1.yw = int2(0, 0);
	r1.xz = r0.yw;
	InterlockedOr(g0[r1.x], r0.z);
	InterlockedAnd(g0[r1.z], 65535);
	r0.y = 0;
	InterlockedCompareStore(g0[r0.x], 0, i.sv_groupid.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = g0[i.sv_groupthreadid.x];
	flags[i.sv_groupthreadid.x] = r0.x;
}
