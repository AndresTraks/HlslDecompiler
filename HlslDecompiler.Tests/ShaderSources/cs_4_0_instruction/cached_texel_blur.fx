cbuffer Blur : register(b0)
{
	float4 weights;
	uint width;
};

static const float4 icb[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(0, 0, 0, 1),
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
	float4 r0;
	float4 r1;
	float4 r2;
	r0.x = i.sv_groupid.x << 6;
	r0.x = r0.x + i.sv_groupindex.x;
	r0.y = (float)i.sv_groupid.y;
	r0.zw = int2(0, 0);
	r1 = source.Load(r0.xyz);
	g0[i.sv_groupindex.x] = r1;
	GroupMemoryBarrierWithGroupSync();
	r1 = float4(0, 0, 0, 0);
	r0.y = 0;
	while (true) {
		r0.z = ((uint)r0.y >= 4) ? -1 : 0;
		if (asint(r0.z) != 0) break;
		r0.z = r0.y + i.sv_groupindex.x;
		r0.z = min(r0.z, 63);
		r2 = g0[r0.z];
		r0.z = dot(weights, icb[r0.y]);
		r1 = r2 * r0.z + r1;
		r0.y = r0.y + 1;
	}
	r0.y = (float)(uint)width;
	r1 = r1 / r0.y;
	output[r0.x] = r1;
}
