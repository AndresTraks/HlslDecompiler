Texture2D depth;
RWStructuredBuffer<float> bounds : register(u0);

groupshared int g0[1];
groupshared int g1[1];

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
		g0[0] = 2139095039;
		g1[0] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.xy = (float2)i.sv_groupthreadid.xy;
	r0.zw = int2(0, 0);
	r0.x = depth.Load(r0.xyz).x;
	InterlockedMin(g0[0], (uint)r0.x);
	InterlockedMax(g1[0], (uint)r0.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = g1[0];
	r0.y = g0[0];
	r0.x = -(r0.y) + r0.x;
	bounds[i.sv_groupindex.x] = r0.x;
}
