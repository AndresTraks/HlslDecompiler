cbuffer Blur : register(b0)
{
	float4 weights;
	uint width;
};

Texture2D source;
RWStructuredBuffer<float4> output : register(u0);

groupshared float4 g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int t0 = (i.sv_groupid.x * 64) + i.sv_groupindex;
	g0[i.sv_groupindex] = source.Load(int3(t0, i.sv_groupid.y, 0));
	GroupMemoryBarrierWithGroupSync();
	float4 t1 = 0;
	for (uint t2 = 0; t2 < 4; t2 = t2 + 1) {
		uint t3 = min(t2 + i.sv_groupindex, 63);
		float t4 = g0[t3].w;
		float t5 = g0[t3].z;
		float t6 = g0[t3].y;
		float t7 = g0[t3].x;
		t1 = t1 + float4(t7, t6, t5, t4) * weights[t2];
	}
	output[t0] = t1 / (float4)width;
}
