RWStructuredBuffer<uint3> output : register(u0);

groupshared int g0[3];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(32, 1, 1)]
void main(CS_IN i)
{
	if (i.sv_groupindex == 0) {
		g0[0] = 3;
		g0[1] = 1065353216;
		g0[2] = 2139095041;
	}
	GroupMemoryBarrierWithGroupSync();
	output[i.sv_dispatchthreadid.x] = int3(i.sv_dispatchthreadid.x + g0[0], g0[1] * i.sv_dispatchthreadid.x, i.sv_dispatchthreadid.x ^ g0[2]);
}
