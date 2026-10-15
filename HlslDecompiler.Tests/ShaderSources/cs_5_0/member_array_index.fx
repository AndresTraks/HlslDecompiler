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
	int t0 = a[sv_dispatchthreadid.x].n & 3;
	float t1 = a[sv_dispatchthreadid.x].w[(t0 + 1 & 3)];
	o[sv_dispatchthreadid.x].w[(a[sv_dispatchthreadid.x].n & 3)] = 2 * t1;
	o[sv_dispatchthreadid.x].n = t0;
}
