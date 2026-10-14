StructuredBuffer<float4> a : register(t0);
StructuredBuffer<uint4> b : register(t1);
RWStructuredBuffer<float4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = a[sv_dispatchthreadid.x];
	bool4 t1 = t0 > 0.5;
	o[sv_dispatchthreadid.x] = 2 * (t1 ? t0 : asfloat(3 * b[sv_dispatchthreadid.x] + 1));
}
