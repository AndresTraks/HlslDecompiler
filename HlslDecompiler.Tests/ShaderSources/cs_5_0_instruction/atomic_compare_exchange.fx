StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<int> ivals : register(u0);
RWStructuredBuffer<int> outbuf : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	int r1;
	r0.y = 0;
	r0.x = idx[sv_dispatchthreadid.x];
	InterlockedCompareExchange(ivals[r0.x], 1, 2, r1);
	outbuf[r0.x] = r1.x;
}
