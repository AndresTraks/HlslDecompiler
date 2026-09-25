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
	float4 r0;
	r0.x = src[sv_dispatchthreadid.x].n;
	r0.w = (float)(uint)r0.x;
	r0.x = src[sv_dispatchthreadid.x].p[1].y;
	r0.y = src[sv_dispatchthreadid.x].v[0].x;
	r0.z = src[sv_dispatchthreadid.x].v[2].z;
	dst[sv_dispatchthreadid.x] = r0;
}
