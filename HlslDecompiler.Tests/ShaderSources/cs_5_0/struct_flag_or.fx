struct P
{
	float3 pos;
	float life;
	float3 vel;
	uint flags;
};

RWStructuredBuffer<P> ps : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = ps[sv_dispatchthreadid.x].life;
	uint t1 = ps[sv_dispatchthreadid.x].flags;
	float t2 = t0 - 0.0160000008;
	float3 t3 = ps[sv_dispatchthreadid.x].pos;
	float3 t4 = ps[sv_dispatchthreadid.x].vel;
	ps[sv_dispatchthreadid.x].pos = 0.0160000008 * t4 + t3;
	ps[sv_dispatchthreadid.x].life = t2;
	ps[sv_dispatchthreadid.x].flags = t1 | (t2 < 0 ? 1 : 0);
}
