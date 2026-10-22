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
	int t0 = sv_dispatchthreadid.x - 4;
	bool t1 = (int)s[0] < t0;
	int t2 = 4 + ((int)(s[sv_dispatchthreadid.x] - 3) >= k ? (2 & ~1) | ((t0 < (int)n ? -1 : 0) & 1) : t0 < (int)n ? 1 : 0);
	o[sv_dispatchthreadid.x] = t1 ? t2 : (int)(s[sv_dispatchthreadid.x] - 3) >= k ? (2 & ~1) | ((t0 < (int)n ? -1 : 0) & 1) : t0 < (int)n ? 1 : 0;
}
