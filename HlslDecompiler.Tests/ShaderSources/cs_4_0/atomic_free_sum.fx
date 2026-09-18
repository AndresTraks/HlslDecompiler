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
	int t0 = (uint)input[i.sv_dispatchthreadid.x] >> 16;
	g0[i.sv_groupindex] = t0 ^ input[i.sv_dispatchthreadid.x];
	GroupMemoryBarrierWithGroupSync();
	int t1 = 0;
	t0 = 0;
	while (true) {
		if (t0 >= 8) {
			break;
		}
		t1 = (g0[t0 + i.sv_groupindex & 63] & 255) + t1;
		t0 = t0 + 1;
	}
	float t2 = (float)count;
	output[i.sv_dispatchthreadid.x] = (float)t1 / t2;
}
