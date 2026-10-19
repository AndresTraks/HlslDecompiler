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
	int4 r0;
	double2 d0;
	double d1;
	r0.x = (uint)sv_dispatchthreadid.y % 3;
	r0.x = r0.x << 3;
	r0.y = -(r0.x) + 24;
	r0.x = r0.x + 8;
	d0.y = a[sv_dispatchthreadid.x].d[(r0.y - 8) / 8];
	d0.y = d0.y * 2;
	r0.y = asint(a[sv_dispatchthreadid.x].f);
	d1 = (double)asfloat(r0.y);
	d0.y = d0.y + d1;
	r0.yz = r0.zw;
	o[sv_dispatchthreadid.x].d[(r0.x - 8) / 8] = d0.y;
}
