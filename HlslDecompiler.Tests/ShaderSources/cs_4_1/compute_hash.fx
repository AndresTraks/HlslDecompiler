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
	int t0 = (uint)1540483477 * (((uint)src[i.sv_dispatchthreadid.x] >> 13) ^ src[i.sv_dispatchthreadid.x]) >> 15;
	dst[i.sv_dispatchthreadid.x] = (t0 ^ 1540483477 * (((uint)src[i.sv_dispatchthreadid.x] >> 13) ^ src[i.sv_dispatchthreadid.x])) ^ i.sv_groupid.x;
}
