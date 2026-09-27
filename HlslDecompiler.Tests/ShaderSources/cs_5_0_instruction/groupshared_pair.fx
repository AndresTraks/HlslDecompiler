StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

struct G0Element
{
	float4 m0;
	float4 m4;
};

groupshared G0Element g0[32];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(32, 1, 1)]
void main(CS_IN i)
{
	int4 r0;
	float4 r1;
	r0 = asint(input[i.sv_dispatchthreadid.x]);
	g0[i.sv_groupindex.x].m0 = asfloat(r0);
	r0 = asint(asfloat(r0) * float4(3, 3, 3, 3));
	g0[i.sv_groupindex.x].m4 = asfloat(r0);
	AllMemoryBarrierWithGroupSync();
	r0.x = i.sv_groupindex.x + 1;
	r0.x = r0.x & 31;
	r1 = g0[r0.x].m0;
	r0 = asint(g0[r0.x].m4);
	r0 = asint(r1 / asfloat(r0));
	output[i.sv_dispatchthreadid.x] = asfloat(r0);
}
