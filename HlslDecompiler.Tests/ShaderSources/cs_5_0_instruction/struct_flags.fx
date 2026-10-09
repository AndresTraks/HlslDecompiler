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
	int4 r0;
	int2 r1;
	float4 r2;
	r0 = asint(float4(ps[sv_dispatchthreadid.x].pos, ps[sv_dispatchthreadid.x].life));
	r1.x = ps[sv_dispatchthreadid.x].flags;
	r1.y = asint(asfloat(r0.w) * dt);
	r2.xyz = asfloat(r1.yyy) * float3(0.5, -9.80000019, 0.25) + asfloat(r0.xyz);
	r2.w = asfloat(r0.w) + -(dt);
	ps[sv_dispatchthreadid.x].pos = r2.xyz;
	ps[sv_dispatchthreadid.x].life = r2.w;
	r0.x = (r2.w < 0) ? -1 : 0;
	r0.y = r1.x | 1;
	r0.x = (r0.x != 0) ? r0.y : r1.x;
	ps[sv_dispatchthreadid.x].flags = r0.x;
}
