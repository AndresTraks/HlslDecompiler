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
	int4 r0;
	float4 r1;
	if (i.sv_groupindex.x == 0) {
		r0.xy = asint(input[0].xy);
		r0.z = asint(input[1].x);
		r0.w = 1065353216;
		g0[0] = r0.x;
		g0[1] = r0.y;
		g0[2] = r0.z;
		g0[3] = r0.w;
		g1[0] = 3;
		g1[1] = 0;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = g0[0];
	r0.y = g0[1];
	r0.z = g0[2];
	r0.w = g0[3];
	r1.x = asfloat(g1[0]);
	r1.y = asfloat(g1[1]);
	r1.z = asfloat(r0.y) + asfloat(r0.x);
	r1.w = asfloat(r0.w) * 3;
	r0 = asint(asfloat(r0.xyzz) * float4(2, 2, 2, 1) + r1);
	output[i.sv_dispatchthreadid.x] = asfloat(r0);
}
