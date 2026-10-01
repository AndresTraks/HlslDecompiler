StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<int> ivals : register(u0);
RWStructuredBuffer<int> outbuf : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = idx[sv_dispatchthreadid.x];
	int t1;
	InterlockedCompareExchange(ivals[t0], 1, 2, t1);
	outbuf[t0] = t1;
}
