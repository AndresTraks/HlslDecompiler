StructuredBuffer<float4> In : register(t0);
RWStructuredBuffer<float4> Out : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	Out[sv_dispatchthreadid.x] = 2 * In[sv_dispatchthreadid.x];
}
