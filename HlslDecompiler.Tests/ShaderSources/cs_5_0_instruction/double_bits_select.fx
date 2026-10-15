StructuredBuffer<uint2> b : register(t0);
RWStructuredBuffer<double> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	double d0;
	float r1;
	r0.xy = b[sv_dispatchthreadid.x];
	r0 = r0.xyyx;
	r1 = (0 < asdouble(asuint(r0.x), asuint(r0.y))) ? -1 : 0;
	d0 = asdouble(asuint(r0.x), asuint(r0.y)) * 3;
	d0 = (asint(r1.x) != 0) ? d0 : asdouble(asuint(r0.z), asuint(r0.w));
	o[sv_dispatchthreadid.x] = d0;
}
