StructuredBuffer<float4> In : register(t0);
RWStructuredBuffer<float4> Out : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float3 r0;
	float4 r1;
	float4 r2;
	r0.x = sv_dispatchthreadid.x << 2;
	r1 = float4(0, 0, 0, 0);
	r0.y = 0;
	while (true) {
		r0.z = (r0.y >= 4) ? -1 : 0;
		if (r0.z != 0) break;
		r0.z = r0.y + r0.x;
		r2 = In[r0.z];
		r1 = r1 + r2;
		r0.y = r0.y + 1;
	}
	Out[sv_dispatchthreadid.x] = r1;
}
