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
	int4 r0;
	r0.x = s[sv_dispatchthreadid.x];
	r0.x = r0.x + -3;
	r0.x = (r0.x >= k) ? -1 : 0;
	r0.y = (int)sv_dispatchthreadid.x + -4;
	r0.z = (r0.y < (int)n) ? -1 : 0;
	r0.w = r0.z & 1;
	r0.z = (2 & ~(((1 << 1) - 1) << 0)) | ((r0.z << 0) & (((1 << 1) - 1) << 0));
	r0.x = (r0.x != 0) ? r0.z : r0.w;
	r0.z = r0.x + 4;
	r0.w = s[0];
	r0.y = (r0.w < r0.y) ? -1 : 0;
	r0.x = (r0.y != 0) ? r0.z : r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
