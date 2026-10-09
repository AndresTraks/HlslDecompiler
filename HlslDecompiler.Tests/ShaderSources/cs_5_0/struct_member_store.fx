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
	float t0 = src[sv_dispatchthreadid.x].b;
	uint2 t1 = src[sv_dispatchthreadid.x].c;
	dst[sv_dispatchthreadid.x].b = 2 * t0;
	dst[sv_dispatchthreadid.x].c = (uint2)((float2)t1.yx + 3);
}
