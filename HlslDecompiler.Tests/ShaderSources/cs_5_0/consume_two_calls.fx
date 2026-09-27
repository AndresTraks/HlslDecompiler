ConsumeStructuredBuffer<float4> queue : register(u0);
RWStructuredBuffer<float4> result : register(u1);

[numthreads(1, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = queue.Consume();
	float4 t1 = queue.Consume();
	result[sv_dispatchthreadid.x] = float4(float2(t0.x, t1.w), 0, 1);
}
