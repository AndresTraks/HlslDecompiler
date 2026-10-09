float dt;

struct P
{
	float3 pos;
	float life;
	uint flags;
};

RWStructuredBuffer<P> ps : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = ps[sv_dispatchthreadid.x].life;
	uint t1 = ps[sv_dispatchthreadid.x].flags;
	float t2 = t0 - dt;
	float t3 = ps[sv_dispatchthreadid.x].pos.x;
	float t4 = ps[sv_dispatchthreadid.x].life * dt;
	float t5 = ps[sv_dispatchthreadid.x].pos.y;
	float t6 = ps[sv_dispatchthreadid.x].pos.z;
	ps[sv_dispatchthreadid.x].pos = float3(0.5, -9.80000019, 0.25) * t4 + float3(t3, t5, t6);
	ps[sv_dispatchthreadid.x].life = t2;
	int t7 = t1 | 1;
	ps[sv_dispatchthreadid.x].flags = t2 < 0 ? t7 : t1;
}
