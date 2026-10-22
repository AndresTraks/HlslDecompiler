cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<int> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = (int)sv_dispatchthreadid.x + -4;
	r0.y = s[sv_dispatchthreadid.x];
	r0.xz = r0.xy >> int2(1, 2);
	r0.y = (uint)r0.y >> 3;
	r0.x = r0.z + r0.x;
	r0.x = r0.y + r0.x;
	r0.y = sv_dispatchthreadid.x & 3;
	r0.y = (int)n >> r0.y;
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
