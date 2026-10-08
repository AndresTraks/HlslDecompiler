StructuredBuffer<uint> values : register(t0);
RWStructuredBuffer<uint> counters : register(u0);
RWStructuredBuffer<uint> output : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0;
	InterlockedAdd(counters[0], values[sv_dispatchthreadid.x], t0);
	int t1;
	InterlockedExchange(counters[1], t0, t1);
	output[sv_dispatchthreadid.x] = t0 + t1;
}
