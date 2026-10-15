Texture2D<float2> t;
RWTexture2D<float4> o : register(u0);

groupshared float g0[8][16];

struct CS_IN
{
	uint3 sv_groupthreadid : SV_GroupThreadID;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	int t0 = i.sv_groupthreadid.x * 8;
	g0[i.sv_groupthreadid.y][(t0) / 4] = t.Load(int3(i.sv_dispatchthreadid.xy, 0)).x;
	g0[i.sv_groupthreadid.y][(t0) / 4 + 1] = t.Load(int3(i.sv_dispatchthreadid.xy, 0)).y;
	GroupMemoryBarrierWithGroupSync();
	int2 t1 = i.sv_groupthreadid.xy + 1;
	int t2 = ((t1.x << 3) & 56);
	float2 t3 = float2(g0[i.sv_groupthreadid.y][(t2) / 4], g0[i.sv_groupthreadid.y][(t2) / 4 + 1]);
	float2 t4 = float2(g0[t1.y & 7][(t0) / 4], g0[t1.y & 7][(t0) / 4 + 1]);
	float2 t5 = t3 + t4;
	o[i.sv_dispatchthreadid.xy] = float4(t5, asfloat(asint(t5.x) ^ i.sv_groupthreadid.x), 1);
}
