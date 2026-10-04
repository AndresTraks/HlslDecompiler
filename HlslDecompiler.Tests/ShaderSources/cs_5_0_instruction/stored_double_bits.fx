StructuredBuffer<double> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double d0;
	d0 = source[sv_dispatchthreadid.x];
	d0 = d0 * 2;
	output[sv_dispatchthreadid.x] = r0.xy;
}
