struct E
{
	uint bits;
	float w;
};

StructuredBuffer<float4> a : register(t0);
RWStructuredBuffer<E> o : register(u0);

[numthreads(32, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	int r1;
	r0 = a[sv_dispatchthreadid.x].xwyz;
	r0.w = r0.w * 100;
	r0.w = (uint)r0.w;
	r1 = (0.5 < r0.x) ? -1 : 0;
	r0.x = (r1.x != 0) ? r0.z : r0.w;
	o[sv_dispatchthreadid.x].bits = r0.x;
	o[sv_dispatchthreadid.x].w = r0.y;
}
