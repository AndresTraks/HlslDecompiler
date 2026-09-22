cbuffer Params : register(b0)
{
	uint2 size;
	float threshold;
	uint step;
};

Texture2D source;
RWTexture2D<float4> luminance : register(u0);
RWStructuredBuffer<uint> histogram : register(u1);

groupshared int g0[64];
groupshared float g1[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	int4 r0;
	int3 r1;
	r0.xy = i.sv_groupid.xy * int2(8, 8) + i.sv_groupthreadid.xy;
	r0.zw = int2(0, 0);
	r1 = asint(source.Load(r0.xyz).xyz);
	r0.z = asint(dot(asfloat(r1.xyz), float3(0.212599993, 0.715200007, 0.0722000003)));
	g1[i.sv_groupindex.x] = asfloat(r0.z);
	r0.w = (threshold < asfloat(r0.z)) ? -1 : 0;
	r0.w = r0.w & 1;
	g0[i.sv_groupindex.x] = r0.w;
	GroupMemoryBarrierWithGroupSync();
	r0.w = 32;
	while (true) {
		r1.x = (0 >= (uint)r0.w) ? -1 : 0;
		if (r1.x != 0) break;
		r1.x = (i.sv_groupindex.x < r0.w) ? -1 : 0;
		if (r1.x != 0) {
			r1.x = r0.w + i.sv_groupindex.x;
			r1.y = asint(g1[r1.x]);
			r1.z = asint(g1[i.sv_groupindex.x]);
			r1.y = asint(asfloat(r1.y) + asfloat(r1.z));
			g1[i.sv_groupindex.x] = asfloat(r1.y);
			r1.x = g0[r1.x];
			r1.y = g0[i.sv_groupindex.x];
			r1.x = r1.x + r1.y;
			g0[i.sv_groupindex.x] = r1.x;
		}
		GroupMemoryBarrierWithGroupSync();
		r0.w = (uint)r0.w >> 1;
	}
	if (i.sv_groupindex.x == 0) {
		r0.w = asint(g1[0]);
		r0.w = asint(asfloat(r0.w) * 0.015625);
		luminance[i.sv_groupid.xy] = asfloat(r0.w);
		r0.w = g0[0];
		r1.x = min(r0.w, 63);
		r1.y = 0;
		InterlockedAdd(histogram[r1.x], 1);
	}
	r1.xy = (r0.xy < size.xy) ? -1 : 0;
	r0.w = r1.y & r1.x;
	r1.x = (step == 0) ? -1 : 0;
	r0.w = r0.w & r1.x;
	if (r0.w != 0) {
		luminance[r0.xy] = asfloat(r0.z);
	}
}
