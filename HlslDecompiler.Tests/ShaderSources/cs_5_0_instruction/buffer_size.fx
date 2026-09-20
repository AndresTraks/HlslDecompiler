StructuredBuffer<float4> source : register(t0);
ByteAddressBuffer raw : register(t1);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	uint2 dimensions0;
	source.GetDimensions(dimensions0.x, dimensions0.y);
	r0.x = dimensions0.x;
	r0.x = r0.x + 16;
	uint dimensions1;
	raw.GetDimensions(dimensions1);
	r0.y = dimensions1;
	output[sv_dispatchthreadid.x] = r0.xy;
}
