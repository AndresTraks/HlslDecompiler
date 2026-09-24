RWStructuredBuffer<float4> written : register(u0);
RWStructuredBuffer<float4> taken : register(u1);
RWStructuredBuffer<float4> output : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	written[written.IncrementCounter()] = float4((float)sv_dispatchthreadid.x, 1, 2, 3);
	output[sv_dispatchthreadid.x] = taken[taken.DecrementCounter()];
}
