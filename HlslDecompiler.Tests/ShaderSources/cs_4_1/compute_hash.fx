StructuredBuffer<uint> src : register(t0);
RWStructuredBuffer<uint> dst : register(u0);

struct CS_IN
{
	uint3 sv_groupid : SV_GroupID;
	uint3 sv_dispatchthreadid : SV_DispatchThreadID;
};

[numthreads(32, 1, 1)]
void main(CS_IN i)
{
	uint t0 = src[i.sv_dispatchthreadid.x];
	uint t1 = 1540483477 * ((t0 >> 13) ^ t0) >> 15;
	dst[i.sv_dispatchthreadid.x] = (t1 ^ 1540483477 * ((t0 >> 13) ^ t0)) ^ i.sv_groupid.x;
}
