Buffer<uint> v;
RWStructuredBuffer<uint2> o : register(u0);

groupshared uint3 g0[8];

struct CS_IN
{
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	if (i.sv_groupthreadid.x < 8) {
		g0[i.sv_groupthreadid.x].xy = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	int t0 = v.Load(64 * i.sv_groupid.x + i.sv_groupthreadid.x).x;
	int t1 = t0 & 7;
	InterlockedAdd(g0[t1].x, 1);
	InterlockedMax(g0[t1].y, t0);
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupthreadid.x < 8) {
		o[i.sv_groupid.x * 8 + i.sv_groupthreadid.x] = g0[i.sv_groupthreadid.x].xy;
	}
}
