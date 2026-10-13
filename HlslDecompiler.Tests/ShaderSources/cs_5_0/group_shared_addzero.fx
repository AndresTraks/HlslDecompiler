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
	int4 t0 = int4(g0[0], g0[1], g0[2], g0[3]);
	output[i.sv_dispatchthreadid.x] = float4(asfloat(t0.x) + asfloat(g1[0]), asfloat(t0.y) + asfloat(g1[1]), asfloat(t0.zw));
	output[i.sv_dispatchthreadid.x + 32] = asfloat(t0);
}
