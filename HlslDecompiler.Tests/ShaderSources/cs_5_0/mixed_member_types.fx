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
	float4 t0 = src[sv_dispatchthreadid.x].b[1].yzwx;
	dst[sv_dispatchthreadid.x].a = src[sv_dispatchthreadid.x].a.zyx;
	dst[sv_dispatchthreadid.x].b[0].x = t0.w;
	dst[sv_dispatchthreadid.x].b[0].yzw = t0.xyz;
	dst[sv_dispatchthreadid.x].b[1].x = (float)(src[sv_dispatchthreadid.x].n + 1);
	dst[sv_dispatchthreadid.x].b[1].yzw = t0.xyz;
	dst[sv_dispatchthreadid.x].n = sv_dispatchthreadid.x;
}
