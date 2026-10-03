struct SrcElement
{
	float3 a;
	float b;
	uint2 c;
};

struct DstElement
{
	float3 a;
	float b;
	uint2 c;
};

StructuredBuffer<SrcElement> src : register(t0);
RWStructuredBuffer<DstElement> dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = src[sv_dispatchthreadid.x].c.x;
	uint t1 = src[sv_dispatchthreadid.x].c.y;
	float t2 = src[sv_dispatchthreadid.x].b;
	dst[sv_dispatchthreadid.x].b = 2 * t2;
	dst[sv_dispatchthreadid.x].c = float2((uint)((float)t1 + 3), (uint)((float)t0 + 3));
}
