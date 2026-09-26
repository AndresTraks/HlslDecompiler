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
	int3 r0;
	r0.x = asint(input[i.sv_dispatchthreadid.x]);
	g0[i.sv_groupindex.x] = asfloat(r0.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = asint(g0[i.sv_groupindex.x]);
	r0.yz = i.sv_groupindex.xx + int2(1, 2);
	r0.yz = r0.yz & int2(63, 63);
	r0.y = asint(g0[r0.y]);
	r0.z = asint(g0[r0.z]);
	r0.x = asint(asfloat(r0.y) + asfloat(r0.x));
	r0.x = asint(asfloat(r0.z) + asfloat(r0.x));
	r0.x = asint(asfloat(r0.x) * 0.333333343);
	output[i.sv_dispatchthreadid.x] = asfloat(r0.x);
}
