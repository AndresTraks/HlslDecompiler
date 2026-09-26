StructuredBuffer<uint> input : register(t0);
RWStructuredBuffer<uint> output : register(u0);

groupshared int g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int3 r0;
	int4 r1;
	r0.x = input[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	r1 = (i.sv_groupindex.x >= int4(1, 2, 4, 8)) ? -1 : 0;
	if (r1.x != 0) {
		r0.y = i.sv_groupindex.x + -1;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	if (r1.y != 0) {
		r0.y = i.sv_groupindex.x + -2;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	if (r1.z != 0) {
		r0.y = i.sv_groupindex.x + -4;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	if (r1.w != 0) {
		r0.y = i.sv_groupindex.x + -8;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	r0.yz = (i.sv_groupindex.xx >= int2(16, 32)) ? -1 : 0;
	if (r0.y != 0) {
		r0.y = i.sv_groupindex.x + -16;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex.x] = r0.x;
	GroupMemoryBarrierWithGroupSync();
	if (r0.z != 0) {
		r0.y = i.sv_groupindex.x + -32;
		r0.y = g0[r0.y];
		r0.x = r0.y + r0.x;
	}
	output[i.sv_dispatchthreadid.x] = r0.x;
}
