RWStructuredBuffer<uint> o : register(u0);

groupshared int g0[1];

[numthreads(64, 1, 1)]
void main(uint3 sv_groupthreadid : SV_GroupThreadID)
{
	int r0;
	if (sv_groupthreadid.x == 0) {
		g0[0] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	InterlockedAdd(g0[0], sv_groupthreadid.x);
	GroupMemoryBarrierWithGroupSync();
	r0 = g0[0];
	o[sv_groupthreadid.x] = r0.x;
}
