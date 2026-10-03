struct SrcElement
{
	float3 p[2];
	float4 v[3];
	uint n;
};

StructuredBuffer<SrcElement> src : register(t0);
RWStructuredBuffer<float4> dst : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = src[sv_dispatchthreadid.x].n;
	dst[sv_dispatchthreadid.x] = float4(src[sv_dispatchthreadid.x].p[1].y, src[sv_dispatchthreadid.x].v[0].x, src[sv_dispatchthreadid.x].v[2].z, (float)t0);
}
