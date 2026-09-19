uint width;

StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

groupshared float4 g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	int t0 = (i.sv_groupid.x * 8) + i.sv_groupthreadid.x + ((i.sv_groupid.y * 8) + i.sv_groupthreadid.y) * width;
	float4 t1 = input[t0];
	g0[i.sv_groupindex] = t1;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupthreadid.x != 0) {
		t1 = t1 + g0[i.sv_groupindex - 1];
	}
	if (i.sv_groupthreadid.x < 7) {
		t1 = t1 + g0[i.sv_groupindex + 1];
	}
	output[(i.sv_groupid.x * 8) + i.sv_groupthreadid.x + ((i.sv_groupid.y * 8) + i.sv_groupthreadid.y) * width] = 0.333333343 * t1;
}
