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
	float4 t3 = float4(ps[sv_dispatchthreadid.x].pos, ps[sv_dispatchthreadid.x].life);
	ps[sv_dispatchthreadid.x].pos = float3(0.5, -9.80000019, 0.25) * t3.w * dt + t3.xyz;
	ps[sv_dispatchthreadid.x].life = t2;
	int t4 = t1 | 1;
	ps[sv_dispatchthreadid.x].flags = t2 < 0 ? t4 : t1;
}
