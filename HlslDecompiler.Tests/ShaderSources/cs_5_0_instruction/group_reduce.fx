StructuredBuffer<float> input : register(t0);
RWStructuredBuffer<float> output : register(u0);

groupshared float g0[128];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(128, 1, 1)]
void main(CS_IN i)
{
	int4 r0;
	int4 r1;
	r0.x = asint(input[i.sv_dispatchthreadid.x]);
	g0[i.sv_groupindex.x] = asfloat(r0.x);
	GroupMemoryBarrierWithGroupSync();
	r1 = (i.sv_groupindex.x < uint4(64, 32, 16, 8)) ? -1 : 0;
	if (r1.x != 0) {
		r0.y = i.sv_groupindex.x + 64;
		r0.y = asint(g0[r0.y]);
		r0.x = asint(asfloat(r0.y) + asfloat(r0.x));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (r1.y != 0) {
		r0.x = i.sv_groupindex.x + 32;
		r0.x = asint(g0[r0.x]);
		r0.y = asint(g0[i.sv_groupindex.x]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (r1.z != 0) {
		r0.x = i.sv_groupindex.x + 16;
		r0.x = asint(g0[r0.x]);
		r0.y = asint(g0[i.sv_groupindex.x]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (r1.w != 0) {
		r0.x = i.sv_groupindex.x + 8;
		r0.x = asint(g0[r0.x]);
		r0.y = asint(g0[i.sv_groupindex.x]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	r0.xyz = (i.sv_groupindex.xxx < uint3(4, 2, 1)) ? -1 : 0;
	if (r0.x != 0) {
		r0.x = i.sv_groupindex.x + 4;
		r0.x = asint(g0[r0.x]);
		r0.w = asint(g0[i.sv_groupindex.x]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.w));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (r0.y != 0) {
		r0.x = i.sv_groupindex.x + 2;
		r0.x = asint(g0[r0.x]);
		r0.y = asint(g0[i.sv_groupindex.x]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
		g0[i.sv_groupindex.x] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (r0.z != 0) {
		r0.x = asint(g0[1]);
		r0.y = asint(g0[0]);
		r0.x = asint(asfloat(r0.x) + asfloat(r0.y));
		g0[0] = asfloat(r0.x);
	}
	GroupMemoryBarrierWithGroupSync();
	if (i.sv_groupindex.x == 0) {
		r0.x = asint(g0[0]);
		output[i.sv_groupid.x] = asfloat(r0.x);
	}
}
