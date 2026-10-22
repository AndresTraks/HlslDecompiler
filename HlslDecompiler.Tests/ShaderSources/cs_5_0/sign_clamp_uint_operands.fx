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
	int t1 = abs(t0) + sign(t0) + abs((int)s[sv_dispatchthreadid.x]) + sign((int)(n - 3));
	o[sv_dispatchthreadid.x] = t1 + clamp((int)(sv_dispatchthreadid.x - 4), -2, k);
}
