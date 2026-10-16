struct S
{
	double2 q[2];
	float f;
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = ((sv_dispatchthreadid.y << 4) & 16);
	double2 t1 = double2(a[sv_dispatchthreadid.x].q[1 - (t0 / 16)].y, a[sv_dispatchthreadid.x].q[1 - (t0 / 16)].x);
	float t2 = a[sv_dispatchthreadid.x].f;
	o[sv_dispatchthreadid.x].q[t0 / 16] = (double2)t2 + t1;
}
