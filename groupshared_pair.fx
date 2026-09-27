StructuredBuffer<float4> input : register(t0);
RWStructuredBuffer<float4> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = 3 * input[sv_dispatchthreadid.x] + 1;
	output[sv_dispatchthreadid.x] = input[sv_dispatchthreadid.x] / t0;
}
