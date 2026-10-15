Buffer<float4> tb;
ByteAddressBuffer rb : register(t1);
StructuredBuffer<float2> sb : register(t2);
RWStructuredBuffer<float4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0;
	tb.GetDimensions(t0);
	uint t1 = t0;
	uint2 t2;
	sb.GetDimensions(t2.x, t2.y);
	uint t3;
	rb.GetDimensions(t3);
	o[sv_dispatchthreadid.x] = float4((float)t1, asfloat(t3), (float)(t2.x * 8), tb.Load(sv_dispatchthreadid.x % t1).x);
}
