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
	g0[i.sv_groupindex] = data[i.sv_dispatchthreadid.x];
	GroupMemoryBarrierWithGroupSync();
	bool t0 = (i.sv_dispatchthreadid.x & levelMask) == 0;
	for (uint t1 = level >> 1; t1 > 0; t1 = t1 >> 1) {
		uint t3 = g0[i.sv_groupindex].x;
		int t4 = g0[i.sv_groupindex].y;
		uint t2 = g0[t1 ^ i.sv_groupindex].x;
		int t5 = g0[t1 ^ i.sv_groupindex].y;
		bool t6 = ((i.sv_groupindex < (t1 ^ i.sv_groupindex)) == t0) == (t2 < t3);
		GroupMemoryBarrierWithGroupSync();
		g0[i.sv_groupindex] = t6 ? int2(t2, t5) : int2(t3, t4);
		GroupMemoryBarrierWithGroupSync();
	}
	data[i.sv_dispatchthreadid.x] = g0[i.sv_groupindex];
}
