StructuredBuffer<float4> a : register(t0);
StructuredBuffer<uint4> b : register(t1);
RWStructuredBuffer<float4> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 r1;
	int4 r2;
	r0 = b[sv_dispatchthreadid.x];
	r0 = r0 * int4(3, 3, 3, 3) + int4(1, 1, 1, 1);
	r1 = a[sv_dispatchthreadid.x];
	r2 = (float4(0.5, 0.5, 0.5, 0.5) < r1) ? -1 : 0;
	r0 = (r2 != 0) ? r1 : r0;
	r0 = r0 + r0;
	o[sv_dispatchthreadid.x] = r0;
}
