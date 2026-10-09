StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared float g0[128];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(128, 1, 1)]
void main(CS_IN i)
{
	float t0 = input[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex] = t0;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 64) {
		t0 = t0 + g0[i.sv_groupindex + 64];
		g0[i.sv_groupindex] = t0;
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 32) {
		t0 = g0[i.sv_groupindex + 32] + g0[i.sv_groupindex];
		g0[i.sv_groupindex] = t0;
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 16) {
		t0 = g0[i.sv_groupindex + 16] + g0[i.sv_groupindex];
		g0[i.sv_groupindex] = t0;
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 8) {
		float t1 = g0[i.sv_groupindex];
		g0[i.sv_groupindex] = g0[i.sv_groupindex + 8] + t1;
	}
	GroupMemoryBarrierWithGroupSync();
	int2 t2 = i.sv_groupindex < uint2(4, 2);
	if (i.sv_groupindex < 4) {
		t2.x = asint(g0[i.sv_groupindex + 4] + g0[i.sv_groupindex]);
		g0[i.sv_groupindex] = asfloat(t2.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 2) {
		t2.y = asint(g0[i.sv_groupindex]);
		t2.x = asint(g0[i.sv_groupindex + 2] + asfloat(t2.y));
		g0[i.sv_groupindex] = asfloat(t2.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex < 1) {
		t2.x = asint(g0[1] + g0[0]);
		g0[0] = asfloat(t2.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex == 0) {
		output[i.sv_groupid.x] = g0[0];
	}
}
