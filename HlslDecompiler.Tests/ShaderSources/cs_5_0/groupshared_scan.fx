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
	int t0 = input[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex >= 1) {
		t0 = g0[i.sv_groupindex - 1] + t0;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex >= 2) {
		t0 = g0[i.sv_groupindex - 2] + t0;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex >= 4) {
		t0 = g0[i.sv_groupindex - 4] + t0;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex >= 8) {
		t0 = g0[i.sv_groupindex - 8] + t0;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	int t1 = i.sv_groupindex >= 16;
	if (i.sv_groupindex >= 16) {
		t1 = g0[i.sv_groupindex - 16];
		t0 = t1 + t0;
	}
	GroupMemoryBarrierWithGroupSync();
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex >= 32) {
		t0 = g0[i.sv_groupindex - 32] + t0;
	}
	output[i.sv_dispatchthreadid.x] = t0;
}
