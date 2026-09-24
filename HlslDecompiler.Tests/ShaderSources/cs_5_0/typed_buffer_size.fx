Buffer<float4> typed;
StructuredBuffer<float4> structured : register(t1);
ByteAddressBuffer raw : register(t2);
RWBuffer<float4> rwtyped : register(u0);
RWStructuredBuffer<uint4> sizes : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0;
	rwtyped.GetDimensions(t0);
	uint t1;
	raw.GetDimensions(t1);
	uint2 t2;
	structured.GetDimensions(t2.x, t2.y);
	uint t3;
	typed.GetDimensions(t3);
	sizes[sv_dispatchthreadid.x] = int4(t3, t2.x, t1, t0);
}
