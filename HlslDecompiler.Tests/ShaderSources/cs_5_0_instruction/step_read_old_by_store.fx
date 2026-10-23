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
	int3 r0;
	r0.x = t[sv_dispatchthreadid.x];
	r0.x = r0.x + -(k);
	r0.y = sv_dispatchthreadid.x + m;
	r0.z = (uint)r0.y >> 1;
	u[sv_dispatchthreadid.x] = r0.z;
	r0.z = ((uint)r0.y < (uint)r0.x) ? -1 : 0;
	r0.x = min((uint)r0.x, r0.y);
	r0.y = r0.z & 1;
	r0.x = r0.y + r0.x;
	t[sv_dispatchthreadid.x] = r0.x;
}
