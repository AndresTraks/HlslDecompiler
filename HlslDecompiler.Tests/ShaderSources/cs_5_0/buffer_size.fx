StructuredBuffer<float4> source : register(t0);
ByteAddressBuffer raw : register(t1);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0;
	raw.GetDimensions(t0);
	uint2 t1;
	source.GetDimensions(t1.x, t1.y);
	output[sv_dispatchthreadid.x] = int2(t1.x + 16, t0);
}
