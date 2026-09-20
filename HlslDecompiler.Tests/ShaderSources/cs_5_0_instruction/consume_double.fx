ConsumeStructuredBuffer<float4> input : register(u0);
RWStructuredBuffer<float4> output : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 r1;
	float4 consumed0 = input.Consume();
	r1.x = consumed0.x;
	r1.y = consumed0.y;
	r1.z = consumed0.z;
	r1.w = consumed0.w;
	r0 = r1 + r1;
	output[sv_dispatchthreadid.x] = r0;
}
