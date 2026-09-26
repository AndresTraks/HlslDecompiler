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
	float2 t0 = float2(g0[i.sv_groupindex + 1 & 63], g0[i.sv_groupindex + 2 & 63]);
	output[i.sv_dispatchthreadid.x] = 0.333333343 * (t0.x + g0[i.sv_groupindex] + t0.y);
}
