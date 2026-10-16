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
	float4 r0;
	double2 d0;
	d0.x = a[sv_dispatchthreadid.x].d[2];
	d0.x = d0.x * 2;
	r0.z = a[sv_dispatchthreadid.x].f;
	d0.y = (double)r0.z;
	d0.x = d0.y + d0.x;
	o[sv_dispatchthreadid.x].d[1] = d0.x;
}
