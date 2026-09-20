StructuredBuffer<float4> In : register(t0);
RWStructuredBuffer<float4> Out : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = sv_dispatchthreadid.x * 4;
	float4 t1 = 0;
	for (uint t2 = 0; t2 < 4; t2 = t2 + 1) {
		t1 = t1 + In[t2 + t0];
	}
	Out[sv_dispatchthreadid.x] = t1;
}
