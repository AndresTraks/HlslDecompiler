uint seed;

StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared float g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	g0[i.sv_groupindex] = input[i.sv_dispatchthreadid.x];
	GroupMemoryBarrierWithGroupSync();
	int t0 = ((uint)i.sv_groupindex >> 2) ^ i.sv_groupindex * seed;
	float t1 = 0;
	for (int t2 = 0; t2 < 4; t2 = t2 + 1) {
		t1 = g0[t2 + t0 & 63] + t1;
	}
	output[i.sv_dispatchthreadid.x] = 0.25 * t1;
}
