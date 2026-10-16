struct L
{
	float3 c;
	float r;
};

struct S
{
	uint n;
	L l[4];
};

StructuredBuffer<S> s : register(t0);
RWStructuredBuffer<float3> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	float4 r1;
	float4 r2;
	int3 r3;
	r0 = int4(s[sv_dispatchthreadid.x].n, asint(s[sv_dispatchthreadid.x].l[0].c));
	r0.x = (0 < (uint)r0.x) ? -1 : 0;
	r1 = float4(s[sv_dispatchthreadid.x].l[0].r, s[sv_dispatchthreadid.x].l[1].c);
	r0.yzw = asint(asfloat(r0.yzw) * r1.xxx);
	r0.xyz = r0.yzw & r0.xxx;
	r2 = float4(s[sv_dispatchthreadid.x].l[1].r, s[sv_dispatchthreadid.x].l[2].c);
	r1.xyz = r1.yzw * r2.xxx + asfloat(r0.xyz);
	r0.w = s[sv_dispatchthreadid.x].n;
	r3 = (uint3(1, 2, 3) < (uint3)r0.www) ? -1 : 0;
	r0.xyz = (r3.xxx != 0) ? asint(r1.xyz) : r0.xyz;
	r1 = float4(s[sv_dispatchthreadid.x].l[2].r, s[sv_dispatchthreadid.x].l[3].c);
	r2.xyz = r2.yzw * r1.xxx + asfloat(r0.xyz);
	r0.xyz = (r3.yyy != 0) ? asint(r2.xyz) : r0.xyz;
	r0.w = asint(s[sv_dispatchthreadid.x].l[3].r);
	r1.xyz = r1.yzw * asfloat(r0.www) + asfloat(r0.xyz);
	r0.xyz = (r3.zzz != 0) ? asint(r1.xyz) : r0.xyz;
	o[sv_dispatchthreadid.x] = asfloat(r0.xyz);
}
