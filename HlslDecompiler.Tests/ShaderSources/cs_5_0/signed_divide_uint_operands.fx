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
	int t1 = s[0];
	uint t0 = abs((int)n);
	int t2 = abs(t1 - 7) / t0;
	uint t3 = abs(k);
	int t4 = (t1 - 7 ^ n) & -2147483648 ? -t2 : t2;
	int t5 = sv_dispatchthreadid.x - 4;
	o[sv_dispatchthreadid.x] = (t5 & -2147483648 ? -(abs(t5) % t0) : abs(t5) % t0) + t5 / (int)s[sv_dispatchthreadid.x] + sv_dispatchthreadid.x % t3 + t4;
}
