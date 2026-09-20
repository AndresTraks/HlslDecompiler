StructuredBuffer<uint> values : register(t0);
RWStructuredBuffer<uint> counters : register(u0);
RWStructuredBuffer<uint> output : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0;
	InterlockedAdd(counters[0], values[sv_dispatchthreadid.x], t0);
	int t1 = t0;
	int t2;
	InterlockedExchange(counters[1], t0, t2);
	int t3 = t2;
	output[sv_dispatchthreadid.x] = t1 + t3;
}
