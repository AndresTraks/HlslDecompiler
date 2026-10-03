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
	uint t0 = src[sv_dispatchthreadid.x].i.b;
	float t1 = src[sv_dispatchthreadid.x].i.a.x;
	float t2 = src[sv_dispatchthreadid.x].i.a.y;
	float t3 = src[sv_dispatchthreadid.x].i.a.z;
	dst[sv_dispatchthreadid.x].i.a = 2 * float3(t3, t2, t1);
	dst[sv_dispatchthreadid.x].i.b = (uint)((float)t0 + 1);
	dst[sv_dispatchthreadid.x].c = src[sv_dispatchthreadid.x].d;
	dst[sv_dispatchthreadid.x].d = src[sv_dispatchthreadid.x].d;
}
