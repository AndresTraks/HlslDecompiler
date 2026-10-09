Texture2D src;
RWTexture2D<float4> dst : register(u0);

groupshared float4 g0[72];

struct CS_IN
{
	uint3 sv_groupthreadid : SV_GroupThreadID;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	uint4 t0 = i.sv_groupthreadid.x + uint4(4, 3, 5, 2);
	g0[t0.x] = src.Load(int3(i.sv_dispatchthreadid.xy, 0));
	if (i.sv_groupthreadid.x < 4) {
		int t1 = i.sv_dispatchthreadid.x - 4;
		int2 t2 = int2(max(t1, 0), i.sv_dispatchthreadid.x + 64);
		g0[i.sv_groupthreadid.x] = src.Load(int3(t2.x, i.sv_dispatchthreadid.y, 0));
		g0[i.sv_groupthreadid.x + 68] = src.Load(int3(t2.y, i.sv_dispatchthreadid.y, 0));
	}
	GroupMemoryBarrierWithGroupSync();
	float4 t3 = g0[i.sv_groupthreadid.x + 8];
	float4 t4 = g0[i.sv_groupthreadid.x] + t3;
	float4 t5 = g0[i.sv_groupthreadid.x + 7];
	float4 t6 = g0[i.sv_groupthreadid.x + 1];
	float4 t7 = g0[i.sv_groupthreadid.x + 6];
	dst[i.sv_dispatchthreadid.xy] = 0.226999998 * g0[t0.x] + 0.194000006 * (g0[t0.y] + g0[t0.z]) + 0.120999999 * (g0[t0.w] + t7) + 0.0540000014 * (t6 + t5) + 0.0160000008 * t4;
}
