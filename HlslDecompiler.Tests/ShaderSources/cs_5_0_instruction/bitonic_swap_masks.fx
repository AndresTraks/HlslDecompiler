cbuffer C : register(b0)
{
	uint level;
	uint levelMask;
};

RWStructuredBuffer<uint2> data : register(u0);

groupshared uint2 g0[256];

struct CS_IN
{
	uint sv_groupindex : SV_GroupIndex;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(256, 1, 1)]
void main(CS_IN i)
{
	int4 r0;
	int4 r1;
	int r2;
	r0.xy = data[i.sv_dispatchthreadid.x];
	g0[i.sv_groupindex.x] = r0.xy;
	GroupMemoryBarrierWithGroupSync();
	r0.x = (uint)level >> 1;
	r0.y = i.sv_dispatchthreadid.x & levelMask;
	r0.y = (r0.y == 0) ? -1 : 0;
	r0.z = r0.x;
	while (true) {
		r0.w = (0 >= (uint)r0.z) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = r0.z ^ i.sv_groupindex.x;
		r1.xy = g0[i.sv_groupindex.x];
		r1.zw = g0[r0.w];
		r2 = ((uint)r1.z < (uint)r1.x) ? -1 : 0;
		r0.w = (i.sv_groupindex.x < (uint)r0.w) ? -1 : 0;
		r0.w = (r0.w == r0.y) ? -1 : 0;
		r0.w = (r0.w == r2.x) ? -1 : 0;
		GroupMemoryBarrierWithGroupSync();
		r1.xy = (r0.ww != 0) ? r1.zw : r1.xy;
		g0[i.sv_groupindex.x] = r1.xy;
		GroupMemoryBarrierWithGroupSync();
		r0.z = (uint)r0.z >> 1;
	}
	r0.xy = g0[i.sv_groupindex.x];
	data[i.sv_dispatchthreadid.x] = r0.xy;
}
