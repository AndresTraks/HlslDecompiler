uint count;

StructuredBuffer<uint> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared int g0[64];

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
	t1 = 0;
	while (t1 < 8) {
		int t3 = g0[t1 + i.sv_groupindex & 63];
		t2 = (t3 & 255) + t2;
		t1 = t1 + 1;
	}
	float t4 = (float)count;
	output[i.sv_dispatchthreadid.x] = (float)t2 / t4;
}
