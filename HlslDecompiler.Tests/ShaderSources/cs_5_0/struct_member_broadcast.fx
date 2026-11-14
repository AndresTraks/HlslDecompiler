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
	float4 t0 = src[sv_dispatchthreadid.x].v;
	float t1 = src[sv_dispatchthreadid.x].w;
	dst[sv_dispatchthreadid.x] = t0 * t1;
}
