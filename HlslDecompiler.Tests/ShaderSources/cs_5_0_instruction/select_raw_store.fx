StructuredBuffer<float4> a : register(t0);
RWByteAddressBuffer o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 r1;
	int4 r2;
	r0 = a[sv_dispatchthreadid.x];
	r1 = r0 * float4(100, 100, 100, 100);
	r1 = (uint4)r1;
	r2 = (float4(0.5, 0.5, 0.5, 0.5) < r0) ? -1 : 0;
	r0 = (r2 != 0) ? r0 : r1;
	r1.x = sv_dispatchthreadid.x << 4;
	o.Store4(r1.x, r0);
}
