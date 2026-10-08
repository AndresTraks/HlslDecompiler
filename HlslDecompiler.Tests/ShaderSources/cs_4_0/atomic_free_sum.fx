uint count;

StructuredBuffer<uint> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared uint g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	uint t0 = input[i.sv_dispatchthreadid.x];
	uint t1 = t0 >> 16;
	g0[i.sv_groupindex] = t1 ^ input[i.sv_dispatchthreadid.x];
	GroupMemoryBarrierWithGroupSync();
	uint t2 = 0;
	for (uint t3 = 0; t3 < 8; t3 = t3 + 1) {
		uint t4 = g0[t3 + i.sv_groupindex & 63];
		t2 = (t4 & 255) + t2;
	}
	float t5 = (float)count;
	output[i.sv_dispatchthreadid.x] = (float)t2 / t5;
}
