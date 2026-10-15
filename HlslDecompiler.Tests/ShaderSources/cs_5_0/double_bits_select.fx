StructuredBuffer<uint2> b : register(t0);
RWStructuredBuffer<double> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	bool t0 = 0 < asdouble(b[sv_dispatchthreadid.x].x, b[sv_dispatchthreadid.x].y);
	uint2 t1 = b[sv_dispatchthreadid.x].yx;
	o[sv_dispatchthreadid.x] = t0 ? asdouble(b[sv_dispatchthreadid.x].x, b[sv_dispatchthreadid.x].y) * 3 : asdouble(t1.x, t1.y);
}
