struct S
{
	float w[4];
	uint n;
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = a[sv_dispatchthreadid.x].n;
	r0.y = r0.x & 3;
	r0.z = r0.y + 1;
	r0.xz = (int2(0, 0) & ~(((1 << int2(2, 2)) - 1) << int2(2, 2))) | ((r0.xz << int2(2, 2)) & (((1 << int2(2, 2)) - 1) << int2(2, 2)));
	r0.z = a[sv_dispatchthreadid.x].w[0];
	r0.z = asint(asfloat(r0.z) + asfloat(r0.z));
	o[sv_dispatchthreadid.x].w[0] = r0.z;
	o[sv_dispatchthreadid.x].n = r0.y;
}
