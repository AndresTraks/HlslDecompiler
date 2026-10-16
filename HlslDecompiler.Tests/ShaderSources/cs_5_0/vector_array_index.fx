struct S
{
	uint k;
	float3 pad;
	float4 v[3];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = a[sv_dispatchthreadid.x].k % 3;
	float4 t1 = a[sv_dispatchthreadid.x].v[t0];
	o[sv_dispatchthreadid.x].v[(t0 + 1) % 3] = 2 * t1;
}
