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
	int4 r0;
	r0.xy = i.sv_dispatchthreadid.xy;
	r0.zw = int2(0, 0);
	r0.xy = asint(t.Load(r0.xyz).xy);
	r0.z = i.sv_groupthreadid.x << 3;
	g0[i.sv_groupthreadid.y][(uint)(r0.zz).x / 4 + 0] = (asfloat(r0.xy)).x;
	g0[i.sv_groupthreadid.y][(uint)(r0.zz).x / 4 + 1] = (asfloat(r0.xy)).y;
	GroupMemoryBarrierWithGroupSync();
	r0.xy = i.sv_groupthreadid.xy + int2(1, 1);
	r0.y = r0.y & 7;
	r0.x = (0 & ~(((1 << 3) - 1) << 3)) | ((r0.x << 3) & (((1 << 3) - 1) << 3));
	r0.xw = asint(float2(g0[i.sv_groupthreadid.y][(uint)(r0.xx).x / 4 + 0], g0[i.sv_groupthreadid.y][(uint)(r0.xx).x / 4 + 1]));
	r0.yz = asint(float2(g0[r0.y][(uint)(r0.zz).x / 4 + 0], g0[r0.y][(uint)(r0.zz).x / 4 + 1]));
	r0.xy = asint(asfloat(r0.yz) + asfloat(r0.xw));
	r0.z = r0.x ^ i.sv_groupthreadid.x;
	r0.w = 1065353216;
	o[i.sv_dispatchthreadid.xy] = asfloat(r0);
}
