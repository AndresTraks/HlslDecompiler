RWStructuredBuffer<uint> o : register(u0);

groupshared int g0;

[numthreads(64, 1, 1)]
void main(uint3 sv_groupthreadid : SV_GroupThreadID)
{
	if (sv_groupthreadid.x == 0) {
		g0 = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	InterlockedAdd(g0, sv_groupthreadid.x);
	GroupMemoryBarrierWithGroupSync();
	o[sv_groupthreadid.x] = g0;
}
