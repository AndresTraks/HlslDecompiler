StructuredBuffer<float4> In : register(t0);
RWStructuredBuffer<float4> Out : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 t0 = 0;
	for (int t1 = 0; t1 < 4; t1 = t1 + 1) {
		t0 = t0 + In[t1 + (sv_dispatchthreadid.x * 4)];
	}
	Out[sv_dispatchthreadid.x] = t0;
}
