StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

groupshared int g0[4];
groupshared int g1[2];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(32, 1, 1)]
void main(CS_IN i)
{
	if (i.sv_groupindex == 0) {
		g0[0] = asuint(input[0].x);
		g0[1] = asuint(input[0].y);
		g0[2] = asuint(input[1].x);
		g0[3] = 1065353216;
		g1[0] = 3;
		g1[1] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	int2 t0 = int2(g1[0], g1[1]);
	int t1 = g0[0];
	int t2 = g0[1];
	int t3 = g0[2];
	output[i.sv_dispatchthreadid.x] = float4(2 * asfloat(t1) + asfloat(t0.x), 2 * asfloat(t2) + asfloat(t0.y), asfloat(t1) + asfloat(t2) + 2 * asfloat(t3), asfloat(t3) + 3 * asfloat(g0[3]));
}
