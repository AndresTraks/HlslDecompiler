StructuredBuffer<uint2> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	double d0;
	r0 = source[sv_dispatchthreadid.x];
	d0 = d0 * 2;
	output[sv_dispatchthreadid.x] = r0.xy;
}
