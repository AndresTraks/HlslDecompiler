struct S
{
	float f;
	int c[5];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.x = asint(a[sv_dispatchthreadid.x].f);
	r0.x = (int)asfloat(r0.x);
	r0.y = (uint)sv_dispatchthreadid.y % 5;
	r0.y = r0.y << 2;
	r0.z = -(r0.y) + 20;
	r0.y = r0.y + 4;
	r0.z = a[sv_dispatchthreadid.x].c[(r0.z - 4) / 4];
	r0.x = r0.x + r0.z;
	o[sv_dispatchthreadid.x].c[(r0.y - 4) / 4] = r0.x;
}
