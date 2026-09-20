uint seed;

StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared float g0[64];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int4 r0;
	r0.x = asint(input[i.sv_dispatchthreadid.x]);
	g0[i.sv_groupindex.x] = asfloat(r0.x);
	GroupMemoryBarrierWithGroupSync();
	r0.x = i.sv_groupindex.x * seed;
	r0.y = (uint)i.sv_groupindex.x >> 2;
	r0.x = r0.y ^ r0.x;
	r0.yz = int2(0, 0);
	while (true) {
		r0.w = ((uint)r0.z >= 4) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = r0.z + r0.x;
		r0.w = r0.w & 63;
		r0.w = asint(g0[r0.w]);
		r0.y = asint(asfloat(r0.w) + asfloat(r0.y));
		r0.z = r0.z + 1;
	}
	r0.x = asint(asfloat(r0.y) * 0.25);
	output[i.sv_dispatchthreadid.x] = asfloat(r0.x);
}
