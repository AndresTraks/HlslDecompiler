struct S
{
	float f;
	uint pad;
	double d[3];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	double t0 = (double)(a[sv_dispatchthreadid.x].f);
	int t1 = sv_dispatchthreadid.y % 3;
	double t2 = a[sv_dispatchthreadid.x].d[2 - t1];
	o[sv_dispatchthreadid.x].d[t1] = t2 * 2 + t0;
}
