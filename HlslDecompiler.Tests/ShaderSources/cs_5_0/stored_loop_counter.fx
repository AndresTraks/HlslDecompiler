cbuffer Compaction : register(b0)
{
	uint itemCount;
	float threshold;
};

StructuredBuffer<float> scores : register(t0);
RWStructuredBuffer<uint> survivors : register(u0);

groupshared int g0[1];
groupshared uint g1[64];

[numthreads(64, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	if (sv_groupindex == 0) {
		g0[0] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	uint t0 = sv_groupindex;
	while (t0 < itemCount) {
		if (scores[t0] < threshold) {
			t0 = t0 + 64;
			continue;
		}
		int t1;
		InterlockedAdd(g0[0], 1, t1);
		if ((uint)t1 < 64) {
			g1[t1] = t0;
		}
		t0 = t0 + 64;
	}
	GroupMemoryBarrierWithGroupSync();
	survivors[sv_groupindex] = g1[sv_groupindex];
}
