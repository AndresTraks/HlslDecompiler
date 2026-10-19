StructuredBuffer<float4> a : register(t0);
RWStructuredBuffer<uint4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int4 r1;
	int4 r2;
	r0 = a[sv_dispatchthreadid.x];
	r1 = asint(r0 * float4(100, 100, 100, 100));
	r1 = (uint4)asfloat(r1);
	r2 = (float4(0.5, 0.5, 0.5, 0.5) < r0) ? -1 : 0;
	r0 = (r2 != 0) ? r0 : asfloat(r1);
	o[sv_dispatchthreadid.x] = r0;
}
