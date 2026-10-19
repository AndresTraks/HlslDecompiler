struct S
{
	float f;
	uint u[4];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = (0 & ~(((1 << 2) - 1) << 2)) | ((sv_dispatchthreadid.y << 2) & (((1 << 2) - 1) << 2));
	r0.x = r0.x + 4;
	r0.y = a[sv_dispatchthreadid.x].u[(r0.x - 4) / 4];
	r0.y = (uint)r0.y >> 1;
	o[sv_dispatchthreadid.x].u[(r0.x - 4) / 4] = r0.y;
	r0.x = asint(a[sv_dispatchthreadid.x].f);
	o[sv_dispatchthreadid.x].f = asfloat(r0.x);
}
