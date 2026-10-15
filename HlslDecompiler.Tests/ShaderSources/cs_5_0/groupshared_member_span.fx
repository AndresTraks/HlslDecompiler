StructuredBuffer<float4> a : register(t0);
RWStructuredBuffer<float4> o : register(u0);

struct G0Element
{
	float4 m0;
	float m4;
};

groupshared G0Element g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	float t0 = a[i.sv_dispatchthreadid.x].w;
	g0[i.sv_groupindex].m0 = float4(a[i.sv_dispatchthreadid.x].xyz, asfloat(asint(t0) ^ i.sv_groupindex));
	g0[i.sv_groupindex].m4 = (float)i.sv_groupindex;
	GroupMemoryBarrierWithGroupSync();
	uint t1 = i.sv_groupindex + 1 & 63;
	float t2 = g0[t1].m4;
	o[i.sv_dispatchthreadid.x] = float4(g0[t1].m0.xyz * t2, g0[t1].m0.w);
}
