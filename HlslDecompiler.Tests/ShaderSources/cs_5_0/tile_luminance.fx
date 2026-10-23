cbuffer Params : register(b0)
{
	uint2 size;
	float threshold;
	uint step;
};

Texture2D source;
RWTexture2D<float> luminance : register(u0);
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
	uint2 t0 = 8 * i.sv_groupid.xy + i.sv_groupthreadid.xy;
	float2 t1 = source.Load(int3(t0, 0)).yz;
	float t2 = dot(float3(source.Load(int3(t0, 0)).x, t1), float3(0.212599993, 0.715200007, 0.0722000003));
	g1[i.sv_groupindex] = t2;
	g0[i.sv_groupindex] = threshold < t2 ? 1 : 0;
	GroupMemoryBarrierWithGroupSync();
	for (uint t3 = 32; t3 > 0; t3 = t3 >> 1) {
		int t4 = i.sv_groupindex < t3;
		if (i.sv_groupindex < t3) {
			uint t5 = t3 + i.sv_groupindex;
			float t6 = g1[i.sv_groupindex];
			g1[i.sv_groupindex] = g1[t5] + t6;
			int t7 = g0[i.sv_groupindex];
			t5 = t7 + g0[t5];
			g0[i.sv_groupindex] = t5;
		}
		GroupMemoryBarrierWithGroupSync();
	}
	if (i.sv_groupindex == 0) {
		float t8 = g1[0];
		luminance[i.sv_groupid.xy] = 0.015625 * t8;
		uint t9 = g0[0];
		InterlockedAdd(histogram[min(t9, 63)], 1);
	}
	if (all(t0 < size) && step == 0) {
		luminance[t0] = t2;
	}
}
