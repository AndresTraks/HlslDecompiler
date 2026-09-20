StructuredBuffer<uint> values : register(t0);
RWStructuredBuffer<uint> counters : register(u0);
RWStructuredBuffer<uint> output : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	int r1;
	r0 = values[sv_dispatchthreadid.x];
	InterlockedAdd(counters[0], r0.x, r0);
	InterlockedExchange(counters[1], r0.x, r1);
	r0 = r0.x + r1.x;
	output[sv_dispatchthreadid.x] = r0.x;
}
