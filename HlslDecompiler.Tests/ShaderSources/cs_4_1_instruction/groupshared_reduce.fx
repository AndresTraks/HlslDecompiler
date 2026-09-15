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
	float3 r0;
	r0.x = i.sv_groupid.x << 6;
	r0.x = r0.x + i.sv_groupindex.x;
	r0.x = input[r0.x];
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	r0.x = 32;
	while (true) {
		r0.y = (0 >= r0.x) ? -1 : 0;
		if (asint(r0.y) != 0) break;
		r0.y = (i.sv_groupindex.x < r0.x) ? -1 : 0;
		if (asint(r0.y) != 0) {
			r0.y = r0.x + i.sv_groupindex.x;
			r0.y = g0[r0.y];
			r0.z = g0[i.sv_groupindex.x];
			r0.y = r0.y + r0.z;
			g0[i.sv_groupindex.x] = r0.y;
		}
		GroupMemoryBarrierWithGroupSync();
		r0.x = (uint)r0.x >> 1;
	}
	if (i.sv_groupindex.x == 0) {
		r0.x = g0[0];
		output[i.sv_groupid.x] = r0.x;
	}
}
