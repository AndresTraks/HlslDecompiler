StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

groupshared int g0[4];

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
		g0[3] = 1065353217;
	}
	GroupMemoryBarrierWithGroupSync();
	int t0 = g0[0];
	int t1 = g0[1];
	int t2 = g0[2];
	int t3 = g0[3];
	output[i.sv_dispatchthreadid.x] = 2 * asfloat(int4(t0, t1, t2, t3));
}
