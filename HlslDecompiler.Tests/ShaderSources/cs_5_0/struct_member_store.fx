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
	float3 t0 = float3(src[sv_dispatchthreadid.x].b, src[sv_dispatchthreadid.x].c);
	dst[sv_dispatchthreadid.x].b = 2 * t0.x;
	dst[sv_dispatchthreadid.x].c = (uint2)((float2)t0.zy + 3);
}
