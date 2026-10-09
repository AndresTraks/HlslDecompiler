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
	float4 t0 = input[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex].m0 = t0;
	g0[i.sv_groupindex].m4 = 3 * t0;
	AllMemoryBarrierWithGroupSync();
	float4 t1 = g0[i.sv_groupindex + 1 & 31].m0;
	uint t2 = i.sv_groupindex + 1 & 31;
	float t3 = g0[t2].m4.w;
	float t4 = g0[t2].m4.x;
	float t5 = g0[t2].m4.y;
	float t6 = g0[t2].m4.z;
	output[i.sv_dispatchthreadid.x] = t1 / float4(t4, t5, t6, t3);
}
