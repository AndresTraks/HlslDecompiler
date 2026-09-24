RWStructuredBuffer<float4> written : register(u0);
RWStructuredBuffer<float4> taken : register(u1);
RWStructuredBuffer<float4> output : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 r1;
	r0.x = written.IncrementCounter();
	r1.x = (float)(uint)sv_dispatchthreadid.x;
	r1.yzw = float3(1, 2, 3);
	written[r0.x] = r1;
	r0.x = taken.DecrementCounter();
	r0 = taken[r0.x];
	output[sv_dispatchthreadid.x] = r0;
}
