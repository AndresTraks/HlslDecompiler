Buffer<float4> typed;
StructuredBuffer<float4> structured : register(t1);
ByteAddressBuffer raw : register(t2);
RWBuffer<float4> rwtyped : register(u0);
RWStructuredBuffer<uint4> sizes : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	uint dimensions0;
	typed.GetDimensions(dimensions0);
	r0.x = dimensions0;
	uint2 dimensions1;
	structured.GetDimensions(dimensions1.x, dimensions1.y);
	r0.y = dimensions1.x;
	uint dimensions2;
	raw.GetDimensions(dimensions2);
	r0.z = dimensions2;
	uint dimensions3;
	rwtyped.GetDimensions(dimensions3);
	r0.w = dimensions3;
	sizes[sv_dispatchthreadid.x] = r0;
}
