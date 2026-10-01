RWBuffer<float4> dst : register(u0);
RWBuffer<float4> src : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	dst[sv_dispatchthreadid.x] = src[sv_dispatchthreadid.x];
}
