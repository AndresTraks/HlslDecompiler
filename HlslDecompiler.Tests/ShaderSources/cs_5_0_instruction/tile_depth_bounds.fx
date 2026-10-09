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
	float4 r0;
	if (i.sv_groupindex.x == 0) {
		g0 = 2139095039;
		g1 = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.xy = (float2)i.sv_groupthreadid.xy;
	r0.zw = int2(0, 0);
	r0.x = depth.Load(r0.xyz).x;
	InterlockedMin(g0, asuint(r0.x));
	InterlockedMax(g1, asuint(r0.x));
	GroupMemoryBarrierWithGroupSync();
	r0.x = asfloat(g1);
	r0.y = asfloat(g0);
	r0.x = -(r0.y) + r0.x;
	bounds[i.sv_groupindex.x] = r0.x;
}
