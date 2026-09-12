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
	int2 r0;
	r0.x = src[i.sv_dispatchthreadid.x];
	r0.y = (uint)r0.x >> 13;
	r0.x = r0.y ^ r0.x;
	r0.x = r0.x * 1540483477;
	r0.y = (uint)r0.x >> 15;
	r0.x = r0.y ^ r0.x;
	r0.x = r0.x ^ i.sv_groupid.x;
	dst[i.sv_dispatchthreadid.x] = r0.x;
}
