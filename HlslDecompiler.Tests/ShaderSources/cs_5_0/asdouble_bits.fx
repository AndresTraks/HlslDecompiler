StructuredBuffer<uint2> source : register(t0);
RWStructuredBuffer<uint2> output : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = source[sv_dispatchthreadid.x].y;
	uint t1 = source[sv_dispatchthreadid.x].x;
	double t2 = asdouble(t1, t0) * 2;
	uint2 t3;
	asuint(t2, t3.x, t3.y);
	output[sv_dispatchthreadid.x] = t3;
}
