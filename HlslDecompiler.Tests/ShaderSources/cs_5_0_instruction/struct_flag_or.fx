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
	int4 r0;
	int4 r1;
	float4 r2;
	r0 = asint(float4(ps[sv_dispatchthreadid.x].pos, ps[sv_dispatchthreadid.x].life));
	r1 = int4(asint(ps[sv_dispatchthreadid.x].vel), ps[sv_dispatchthreadid.x].flags);
	r2.xyz = asfloat(r1.xyz) * float3(0.0160000008, 0.0160000008, 0.0160000008) + asfloat(r0.xyz);
	r2.w = asfloat(r0.w) + -0.0160000008;
	ps[sv_dispatchthreadid.x].pos = r2.xyz;
	ps[sv_dispatchthreadid.x].life = r2.w;
	r0.x = (r2.w < 0) ? -1 : 0;
	r0.x = r0.x & 1;
	r0.x = r0.x | r1.w;
	ps[sv_dispatchthreadid.x].flags = r0.x;
}
