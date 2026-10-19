Texture2D<float> depth;
RWStructuredBuffer<float> bounds : register(u0);

groupshared uint g0;
groupshared uint g1;

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	int4 r0;
	if (i.sv_groupindex.x == 0) {
		g0 = 2139095039;
		g1 = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.xy = i.sv_groupthreadid.xy;
	r0.zw = int2(0, 0);
	r0.x = asint(depth.Load(r0.xyz).x);
	InterlockedMin(g0, (uint)r0.x);
	InterlockedMax(g1, (uint)r0.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = g1;
	r0.y = g0;
	r0.x = asint(-(asfloat(r0.y)) + asfloat(r0.x));
	bounds[i.sv_groupindex.x] = asfloat(r0.x);
}
