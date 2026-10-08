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
	float2 r0;
	int r1;
	if (sv_groupindex.x == 0) {
		g0[0] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = (float)sv_groupindex.x;
	while (true) {
		r0.y = ((uint)r0.x >= itemCount) ? -1 : 0;
		if (asint(r0.y) != 0) break;
		r0.y = scores[r0.x];
		r0.y = (r0.y < threshold) ? -1 : 0;
		if (asint(r0.y) != 0) {
			r0.y = r0.x + 64;
			r0.x = r0.y;
			continue;
		}
		InterlockedAdd(g0[0], 1, r1);
		r0.y = ((uint)r1.x < 64) ? -1 : 0;
		if (asint(r0.y) != 0) {
			g1[r1.x] = r0.x;
		}
		r0.x = r0.x + 64;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = g1[sv_groupindex.x];
	survivors[sv_groupindex.x] = r0.x;
}
