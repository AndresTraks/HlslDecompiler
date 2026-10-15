StructuredBuffer<uint2> a : register(t0);
RWStructuredBuffer<float> o : register(u0);
RWStructuredBuffer<float2> o2 : register(u1);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	o2[sv_dispatchthreadid.x] = asfloat(a[sv_dispatchthreadid.x] ^ -2147483648);
	uint2 t0 = a[sv_dispatchthreadid.x];
	o[sv_dispatchthreadid.x] = asfloat(t0.x / t0.y);
}
