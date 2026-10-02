ConsumeStructuredBuffer<float4> queue : register(u0);
RWStructuredBuffer<float4> result : register(u1);

[numthreads(1, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 consumed0 = queue.Consume();
	float4 consumed1 = queue.Consume();
	r0.x = consumed0.x;
	r0.y = consumed1.w;
	r0.zw = float2(0, 1);
	result[sv_dispatchthreadid.x] = r0;
}
