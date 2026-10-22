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
	int t0 = sv_dispatchthreadid.x - 6;
	int t1 = s[0];
	int t2 = min(t1, t0);
	int t3 = sv_dispatchthreadid.x - 4;
	int t4 = s[sv_dispatchthreadid.x];
	o[sv_dispatchthreadid.x] = max(t4, k) + min(t3, (int)n) + t2;
}
