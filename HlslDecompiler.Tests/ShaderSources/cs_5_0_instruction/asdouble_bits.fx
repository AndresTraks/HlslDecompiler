StructuredBuffer<uint2> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double d0;
	d0 = asdouble(source[sv_dispatchthreadid.x].x, source[sv_dispatchthreadid.x].y);
	d0 = d0 * 2;
	asuint(d0, output[sv_dispatchthreadid.x].x, output[sv_dispatchthreadid.x].y);
}
