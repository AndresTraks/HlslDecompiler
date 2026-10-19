struct struct1
{
	float3 a;
	uint b;
};

struct SrcElement
{
	struct1 i;
	float2 c;
	float2 d;
};

struct DstElement
{
	struct1 i;
	float2 c;
	float2 d;
};

StructuredBuffer<SrcElement> src : register(t0);
RWStructuredBuffer<DstElement> dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int4 r1;
	r0 = int4(asint(src[sv_dispatchthreadid.x].i.a), src[sv_dispatchthreadid.x].i.b);
	r0.w = asint((float)(uint)r0.w);
	r1.xyz = asint(asfloat(r0.zyx) + asfloat(r0.zyx));
	r0.x = asint(asfloat(r0.w) + 1);
	r1.w = (uint)asfloat(r0.x);
	dst[sv_dispatchthreadid.x].i.a = asfloat(r1.xyz);
	dst[sv_dispatchthreadid.x].i.b = r1.w;
	r0.xy = asint(src[sv_dispatchthreadid.x].d);
	dst[sv_dispatchthreadid.x].c = asfloat(r0.xy);
	dst[sv_dispatchthreadid.x].d = asfloat(r0.xy);
}
