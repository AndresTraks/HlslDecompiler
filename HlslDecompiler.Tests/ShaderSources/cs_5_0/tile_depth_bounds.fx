Texture2D depth;
RWStructuredBuffer<float> bounds : register(u0);

groupshared uint g0[1];
groupshared uint g1[1];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	if (i.sv_groupindex == 0) {
		g0[0] = 2139095039;
		g1[0] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	float t0 = depth.Load(int3(i.sv_groupthreadid.xy, 0)).x;
	InterlockedMin(g0[0], asuint(t0));
	InterlockedMax(g1[0], asuint(t0));
	GroupMemoryBarrierWithGroupSync();
	int t1 = g0[0];
	bounds[i.sv_groupindex] = asfloat(g1[0]) - asfloat(t1);
}
