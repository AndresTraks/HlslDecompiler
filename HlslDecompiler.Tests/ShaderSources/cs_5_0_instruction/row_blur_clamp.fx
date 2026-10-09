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
	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0 = i.sv_groupthreadid.x + int4(4, 3, 5, 2);
	r1.yz = (float2)i.sv_dispatchthreadid.xy;
	r1.w = 0;
	r2 = src.Load(r1.yzw);
	g0[r0.x] = r2;
	r2.x = (i.sv_groupthreadid.x < 4) ? -1 : 0;
	if (asint(r2.x) != 0) {
		r1.xy = (int2)i.sv_dispatchthreadid.xx + int2(-4, 64);
		r1.x = max(r1.x, 0);
		r2 = src.Load(r1.xzw);
		g0[i.sv_groupthreadid.x] = r2;
		r1.x = i.sv_groupthreadid.x + 68;
		r2 = src.Load(r1.yzw);
		g0[r1.x] = r2;
	}
	GroupMemoryBarrierWithGroupSync();
	r1 = g0[r0.x];
	r2 = g0[r0.y];
	r3 = g0[r0.z];
	r2 = r2 + r3;
	r2 = r2 * float4(0.194000006, 0.194000006, 0.194000006, 0.194000006);
	r1 = r1 * float4(0.226999998, 0.226999998, 0.226999998, 0.226999998) + r2;
	r0 = g0[r0.w];
	r2 = i.sv_groupthreadid.x + int4(6, 1, 7, 8);
	r3 = g0[r2.x];
	r0 = r0 + r3;
	r0 = r0 * float4(0.120999999, 0.120999999, 0.120999999, 0.120999999) + r1;
	r1 = g0[r2.y];
	r3 = g0[r2.z];
	r1 = r1 + r3;
	r0 = r1 * float4(0.0540000014, 0.0540000014, 0.0540000014, 0.0540000014) + r0;
	r1 = g0[i.sv_groupthreadid.x];
	r2 = g0[r2.w];
	r1 = r1 + r2;
	r0 = r1 * float4(0.0160000008, 0.0160000008, 0.0160000008, 0.0160000008) + r0;
	dst[i.sv_dispatchthreadid.xy] = r0;
}
