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
	float4 t0 = float4(src[sv_dispatchthreadid.x].i.a, src[sv_dispatchthreadid.x].i.b);
	dst[sv_dispatchthreadid.x].i.a = 2 * t0.zyx;
	dst[sv_dispatchthreadid.x].i.b = (uint)((float)t0.w + 1);
	dst[sv_dispatchthreadid.x].c = src[sv_dispatchthreadid.x].d;
	dst[sv_dispatchthreadid.x].d = src[sv_dispatchthreadid.x].d;
}
