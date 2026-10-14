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
	float4 r0;
	if (i.sv_groupindex.x == 0) {
		r0.xy = input[0].xy;
		r0.z = input[1].x;
		r0.w = 1065353217;
		g0[0] = asint(r0.x);
		g0[1] = asint(r0.y);
		g0[2] = asint(r0.z);
		g0[3] = r0.w;
	}
	GroupMemoryBarrierWithGroupSync();
	r0.x = asfloat(g0[0]);
	r0.y = asfloat(g0[1]);
	r0.z = asfloat(g0[2]);
	r0.w = asfloat(g0[3]);
	r0 = r0 + r0;
	output[i.sv_dispatchthreadid.x] = r0;
}
