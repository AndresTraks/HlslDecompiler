StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

struct G0Element
{
	float4 m0;
	float m4;
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
	g0[i.sv_groupindex].m0 = input[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex].m4 = (float)(i.sv_dispatchthreadid.x + 7);
	AllMemoryBarrierWithGroupSync();
	float4 t0 = g0[i.sv_groupindex + 1 & 31].m0;
	float t1 = g0[i.sv_groupindex + 1 & 31].m4;
	output[i.sv_dispatchthreadid.x] = t0 / t1;
}
