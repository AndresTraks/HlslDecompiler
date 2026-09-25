struct SrcElement
{
	float3 a;
	float4 b[2];
	uint n;
};

struct DstElement
{
	float3 a;
	float4 b[2];
	uint n;
};

StructuredBuffer<SrcElement> src : register(t0);
RWStructuredBuffer<DstElement> dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float4 r1;
	r0.xyz = src[sv_dispatchthreadid.x].a;
	r1 = src[sv_dispatchthreadid.x].b[1].yzwx;
	r0.w = r1.w;
	dst[sv_dispatchthreadid.x].a = r0.zyx;
	dst[sv_dispatchthreadid.x].b[0].x = r0.w;
	r0.x = src[sv_dispatchthreadid.x].n;
	r0.x = r0.x + 1;
	r1.w = (float)(uint)r0.x;
	dst[sv_dispatchthreadid.x].b[0].yzw = r1.xyz;
	dst[sv_dispatchthreadid.x].b[1].x = r1.w;
	r1.w = (float)sv_dispatchthreadid.x;
	dst[sv_dispatchthreadid.x].b[1].yzw = r1.xyz;
	dst[sv_dispatchthreadid.x].n = r1.w;
}
