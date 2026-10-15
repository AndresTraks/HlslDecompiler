StructuredBuffer<uint2> a : register(t0);
RWStructuredBuffer<float2> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint2 t0 = a[sv_dispatchthreadid.x];
	uint t1 = t0.x % t0.y;
	o[sv_dispatchthreadid.x] = float2(asfloat(t0.x / t0.y), (float)t1);
}
