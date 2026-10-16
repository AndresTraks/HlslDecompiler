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
	int2 r0;
	float4 r1;
	r0.x = a[sv_dispatchthreadid.x].k;
	r0.x = (uint)r0.x % 3;
	r0.y = r0.x << 4;
	r0.x = r0.x + 1;
	r0.x = (uint)r0.x % 3;
	r0.x = r0.x << 4;
	r0 = r0.xy + int2(16, 16);
	r1 = a[sv_dispatchthreadid.x].v[(r0.y - 16) / 16];
	r1 = r1 + r1;
	o[sv_dispatchthreadid.x].v[(r0.x - 16) / 16] = r1;
}
