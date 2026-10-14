StructuredBuffer<float4> a : register(t0);
RWStructuredBuffer<uint4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = a[sv_dispatchthreadid.x];
	uint4 t1 = (uint4)(100 * t0);
	bool4 t2 = t0 > 0.5;
	o[sv_dispatchthreadid.x] = t2 ? asint(t0) : t1;
}
