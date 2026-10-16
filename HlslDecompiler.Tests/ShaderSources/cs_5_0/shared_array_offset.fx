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
	int t0 = ((sv_dispatchthreadid.y << 2) & 12) + 4;
	uint t1 = a[sv_dispatchthreadid.x].u[(t0 - 4) / 4];
	o[sv_dispatchthreadid.x].u[(t0 - 4) / 4] = t1 >> 1;
	o[sv_dispatchthreadid.x].f = a[sv_dispatchthreadid.x].f;
}
