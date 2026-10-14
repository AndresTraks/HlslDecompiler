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
	int4 r0;
	if (i.sv_groupindex.x == 0) {
		g0[0] = 3;
		g0[1] = 1065353216;
		g0[2] = 2139095041;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = g0[0];
	r0.x = r0.x + i.sv_dispatchthreadid.x;
	r0.w = g0[1];
	r0.y = r0.w * i.sv_dispatchthreadid.x;
	r0.w = g0[2];
	r0.z = r0.w ^ i.sv_dispatchthreadid.x;
	output[i.sv_dispatchthreadid.x] = r0.xyz;
}
