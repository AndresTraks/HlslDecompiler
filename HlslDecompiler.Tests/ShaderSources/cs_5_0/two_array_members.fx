struct S
{
	float w[4];
	uint m[2];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = ((sv_dispatchthreadid.y << 2) & 12);
	float t1 = a[sv_dispatchthreadid.x].w[3 - (t0 / 4)];
	uint t2 = a[sv_dispatchthreadid.x].m[(sv_dispatchthreadid.y & 1)];
	o[sv_dispatchthreadid.x].w[t0 / 4] = (float)t2 + t1;
}
