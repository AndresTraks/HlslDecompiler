StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared float g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	g0[i.sv_groupindex] = input[(i.sv_groupid.x * 64) + i.sv_groupindex];
	GroupMemoryBarrierWithGroupSync();
	for (uint t0 = 32; t0 > 0; t0 = t0 >> 1) {
		float t1;
		if (i.sv_groupindex < t0) {
			t1 = g0[i.sv_groupindex];
			g0[i.sv_groupindex] = g0[t0 + i.sv_groupindex] + t1;
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if (i.sv_groupindex == 0) {
		output[i.sv_groupid.x] = g0[0];
	}
}
