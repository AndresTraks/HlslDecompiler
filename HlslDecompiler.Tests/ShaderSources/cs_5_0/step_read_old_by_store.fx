cbuffer C : register(b0)
{
	int k;
	uint m;
};

RWStructuredBuffer<uint> t : register(u0);
RWStructuredBuffer<uint> u : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint2 t0 = uint2(t[sv_dispatchthreadid.x] - k, sv_dispatchthreadid.x + m);
	u[sv_dispatchthreadid.x] = t0.y >> 1;
	t[sv_dispatchthreadid.x] = min(t0.x, t0.y) + (t0.y < t0.x ? 1 : 0);
}
