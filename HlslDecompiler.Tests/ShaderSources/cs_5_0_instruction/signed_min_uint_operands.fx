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
	r0.x = s[sv_dispatchthreadid.x];
	r0.x = max(r0.x, k);
	r0.yz = (int2)sv_dispatchthreadid.xx + int2(-4, -6);
	r0.y = min(r0.y, (int)n);
	r0.x = r0.x + r0.y;
	r0.y = s[0];
	r0.y = min(r0.y, r0.z);
	r0.x = r0.y + r0.x;
	o[sv_dispatchthreadid.x] = r0.x;
}
