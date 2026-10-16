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
	float4 t0 = float4(s[sv_dispatchthreadid.x].l[0].r, s[sv_dispatchthreadid.x].l[1].c);
	float4 t1 = float4(s[sv_dispatchthreadid.x].l[1].r, s[sv_dispatchthreadid.x].l[2].c);
	float4 t2 = float4(s[sv_dispatchthreadid.x].l[2].r, s[sv_dispatchthreadid.x].l[3].c);
	float3 t3 = s[sv_dispatchthreadid.x].l[0].c;
	float t4 = s[sv_dispatchthreadid.x].l[3].r;
	uint t5 = s[sv_dispatchthreadid.x].n;
	float3 t6 = t5 > 0 ? t3 * t0.x : 0;
	uint t7 = t5;
	float3 t8 = t7 > 1 ? t0.yzw * t1.x + t6 : t6;
	float3 t9 = t2.yzw * t4 + (t7 > 2 ? t1.yzw * t2.x + t8 : t8);
	bool t10 = t7 > 3;
	o[sv_dispatchthreadid.x] = t10 ? t9 : t7 > 2 ? t1.yzw * t2.x + t8 : t8;
}
