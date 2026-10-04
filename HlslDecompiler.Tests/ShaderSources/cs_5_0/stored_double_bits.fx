StructuredBuffer<double> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = source[sv_dispatchthreadid.x];
	double t1 = t0 * 2;
	uint2 t2;
	asuint(t1, t2.x, t2.y);
	output[sv_dispatchthreadid.x] = t2;
}
