ConsumeStructuredBuffer<float4> input : register(u0);
RWStructuredBuffer<float4> output : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = input.Consume();
	float t1 = t0.x;
	output[sv_dispatchthreadid.x] = 2 * float4(t1, t0.yzw);
}
