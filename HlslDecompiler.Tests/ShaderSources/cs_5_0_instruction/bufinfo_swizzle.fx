Buffer<float4> tb;
ByteAddressBuffer rb : register(t1);
StructuredBuffer<float2> sb : register(t2);
RWStructuredBuffer<float4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float2 r1;
	uint2 dimensions0;
	sb.GetDimensions(dimensions0.x, dimensions0.y);
	r0.x = dimensions0.x;
	r0.x = (int)r0.x << 3;
	r0.z = (float)(uint)r0.x;
	uint dimensions1;
	rb.GetDimensions(dimensions1);
	r0.y = dimensions1;
	uint dimensions2;
	tb.GetDimensions(dimensions2);
	r1.x = dimensions2;
	r1.y = (uint)sv_dispatchthreadid.x % (uint)r1.x;
	r0.x = (float)(uint)r1.x;
	r0.w = tb.Load(r1.y).x;
	o[sv_dispatchthreadid.x] = r0;
}
