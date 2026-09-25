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
	float4 r0;
	float4 r1;
	r0 = float4(src[sv_dispatchthreadid.x].i.a, src[sv_dispatchthreadid.x].i.b);
	r0.w = (float)(uint)r0.w;
	r1.xyz = r0.zyx + r0.zyx;
	r0.x = r0.w + 1;
	r1.w = (uint)r0.x;
	dst[sv_dispatchthreadid.x].i.a = r1.xyz;
	dst[sv_dispatchthreadid.x].i.b = r1.w;
	r0.xy = src[sv_dispatchthreadid.x].d;
	dst[sv_dispatchthreadid.x].c = r0.xy;
	dst[sv_dispatchthreadid.x].d = r0.xy;
}
