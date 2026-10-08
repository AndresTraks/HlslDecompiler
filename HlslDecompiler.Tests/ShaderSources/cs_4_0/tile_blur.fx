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
	int2 t1 = (i.sv_groupid.xy * 8) + i.sv_groupthreadid.xy;
	uint t0 = t1.y * width + t1.x;
	float4 t2 = input[t0];
	g0[i.sv_groupindex] = t2;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupthreadid.x != 0) {
		t2 = t2 + g0[i.sv_groupindex - 1];
	}
	if (i.sv_groupthreadid.x < 7) {
		t2 = t2 + g0[i.sv_groupindex + 1];
	}
	output[t0] = 0.333333343 * t2;
}
