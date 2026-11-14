struct E
{
	float4 v;
	float w;
};

StructuredBuffer<E> src : register(t0);
RWStructuredBuffer<float4> dst : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	float r1;
	r0 = src[sv_dispatchthreadid.x].v;
	r1 = src[sv_dispatchthreadid.x].w;
	r0 = r0 * r1.x;
	dst[sv_dispatchthreadid.x] = r0;
}
