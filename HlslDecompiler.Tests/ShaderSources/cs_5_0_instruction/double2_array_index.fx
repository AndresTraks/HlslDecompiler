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
	int4 r0;
	double d0;
	int4 r1;
	double2 d1;
	double2 d2;
	r0.x = asint(a[sv_dispatchthreadid.x].f);
	d0 = (double)asfloat(r0.x);
	r0.z = (0 & ~(1 << 4)) | ((sv_dispatchthreadid.y << 4) & (1 << 4));
	r0.w = -(r0.z) + 16;
	r1.x = r0.w + 8;
	d1.y = a[sv_dispatchthreadid.x].q[r0.w / 16].x;
	d2.y = d1.y;
	d2.x = a[sv_dispatchthreadid.x].q[r1.x / 16].y;
	d1 = d0 + d2;
	o[sv_dispatchthreadid.x].q[r0.z / 16] = d1;
}
