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
	int t0 = ((sv_dispatchthreadid.y << 3) & 24);
	float t1 = a[sv_dispatchthreadid.x].p[t0 / 8].y;
	float t2 = a[sv_dispatchthreadid.x].p[t0 / 8].x;
	float t3 = a[sv_dispatchthreadid.x].s;
	o[sv_dispatchthreadid.x].p[t0 / 8] = float2(t1, t2) * t3;
}
