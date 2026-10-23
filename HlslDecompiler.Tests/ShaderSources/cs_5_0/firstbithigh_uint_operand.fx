cbuffer C : register(b0)
{
	uint n;
	int k;
};

StructuredBuffer<uint> s : register(t0);
RWStructuredBuffer<uint> o : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = firstbithigh((int)(k - sv_dispatchthreadid.x));
	uint t1 = k - sv_dispatchthreadid.x;
	o[sv_dispatchthreadid.x] = (t1 >> 3) + max(s[sv_dispatchthreadid.x], t1) + (t1 < n ? 1 : 0) + t1 / 5 + firstbithigh(t1) + t0;
}
