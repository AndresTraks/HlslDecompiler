struct BufElement
{
	int v;
	uint u;
};

StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<BufElement> buf : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = idx[sv_dispatchthreadid.x];
	int t1;
	InterlockedCompareExchange(buf[t0 & 7].v, 1, 2, t1);
	buf[t0].u = t1;
}
