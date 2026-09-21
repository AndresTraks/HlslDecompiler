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
	uint2 t2 = 8 * i.sv_groupid.xy + i.sv_groupthreadid.xy;
	float t1 = source.Load(int3(t2, 0)).y;
	float t0 = source.Load(int3(t2, 0)).z;
	float t3 = 0.212599993 * source.Load(int3(t2, 0)).x + 0.715200007 * t1 + 0.0722000003 * t0;
	g1[i.sv_groupindex] = t3;
	g0[i.sv_groupindex] = threshold < t3 ? 1 : 0;
	GroupMemoryBarrierWithGroupSync();
	for (uint t4 = 32; t4 > 0; t4 = t4 >> 1) {
		int t5 = i.sv_groupindex < t4;
		if (i.sv_groupindex < t4) {
			t5 = t4 + i.sv_groupindex;
			t0 = g1[i.sv_groupindex];
			g1[i.sv_groupindex] = g1[t5] + t0;
			t1 = g0[i.sv_groupindex];
			t5 = g0[t5] + t1;
			g0[i.sv_groupindex] = t5;
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if (i.sv_groupindex == 0) {
		luminance[i.sv_groupid.xy] = 0.015625 * g1[0];
		InterlockedAdd(histogram[min(g0[0], 63)], 1);
	}
	if ((all(t2 < size) && step == 0) != 0) {
		luminance[t2] = t3;
	}
}
