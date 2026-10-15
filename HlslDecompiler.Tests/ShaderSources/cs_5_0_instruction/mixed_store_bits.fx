StructuredBuffer<uint2> a : register(t0);
RWStructuredBuffer<float2> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	int r1;
	r0 = a[sv_dispatchthreadid.x];
	uint dividend0 = (uint)r0.x;
	uint divisor1 = (uint)r0.y;
	r0.x = dividend0 / divisor1;
	r1 = dividend0 % divisor1;
	r0.y = (float)(uint)r1.x;
	o[sv_dispatchthreadid.x] = r0.xy;
}
