struct S
{
	float2 p[4];
	float s;
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	r0.x = asint(a[sv_dispatchthreadid.x].s);
	r0.y = (0 & ~(3 << 3)) | ((sv_dispatchthreadid.y << 3) & (3 << 3));
	r0.zw = asint(a[sv_dispatchthreadid.x].p[r0.y / 8]);
	r0.xz = asint(asfloat(r0.xx) * asfloat(r0.wz));
	o[sv_dispatchthreadid.x].p[r0.y / 8] = asfloat(r0.xz);
}
