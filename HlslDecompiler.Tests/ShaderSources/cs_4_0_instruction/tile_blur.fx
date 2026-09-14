uint width;

StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

groupshared float4 g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(8, 8, 1)]
void main(CS_IN i)
{
	int2 r0;
	float4 r1;
	float4 r2;
	r0 = i.sv_groupid.xy << 3;
	r0 = r0.xy + i.sv_groupthreadid.xy;
	r0.x = r0.y * width + r0.x;
	r1 = input[r0.x];
	g0[i.sv_groupindex.x] = r1;
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupthreadid.x != 0) {
		r0.y = i.sv_groupindex.x + -1;
		r2 = g0[r0.y];
		r1 = r1 + r2;
	}
	r0.y = (i.sv_groupthreadid.x < 7) ? -1 : 0;
	if (r0.y != 0) {
		r0.y = i.sv_groupindex.x + 1;
		r2 = g0[r0.y];
		r1 = r1 + r2;
	}
	r1 = r1 * float4(0.333333343, 0.333333343, 0.333333343, 0.333333343);
	output[r0.x] = r1;
}
