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
	int t0 = a[sv_dispatchthreadid.x].c[4 - (sv_dispatchthreadid.y % 5)];
	o[sv_dispatchthreadid.x].c[sv_dispatchthreadid.y % 5] = t0 + (int)(a[sv_dispatchthreadid.x].f);
}
