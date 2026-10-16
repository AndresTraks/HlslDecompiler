Buffer<uint> v;
RWStructuredBuffer<uint2> o : register(u0);

groupshared uint3 g0[8];

struct CS_IN
{
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_groupthreadid : SV_GroupThreadID;
};

[numthreads(64, 1, 1)]
void main(CS_IN i)
{
	int3 r0;
	int3 r1;
	r0.x = (i.sv_groupthreadid.x < 8) ? -1 : 0;
	if (r0.x != 0) {
		g0[i.sv_groupthreadid.x].xy = int2(0, 0);
	}
	GroupMemoryBarrierWithGroupSync();
	r0.y = i.sv_groupid.x * 64 + i.sv_groupthreadid.x;
	r0.y = v.Load(r0.y).x;
	r1.x = r0.y & 7;
	r1.yz = int2(0, 4);
	InterlockedAdd(g0[r1.x].x, 1);
	InterlockedMax(g0[r1.x].y, (uint)r0.y);
	GroupMemoryBarrierWithGroupSync();
	if (r0.x != 0) {
		r0.x = i.sv_groupid.x << 3;
		r0.x = r0.x + i.sv_groupthreadid.x;
		r0.yz = g0[i.sv_groupthreadid.x].xy;
		o[r0.x] = r0.yz;
	}
}
