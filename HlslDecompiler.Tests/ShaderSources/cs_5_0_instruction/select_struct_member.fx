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
	int4 r0;
	int r1;
	r0 = asint(a[sv_dispatchthreadid.x].xwyz);
	r0.w = asint(asfloat(r0.w) * 100);
	r0.w = (uint)asfloat(r0.w);
	r1 = (0.5 < asfloat(r0.x)) ? -1 : 0;
	r0.x = (r1.x != 0) ? r0.z : r0.w;
	o[sv_dispatchthreadid.x].bits = r0.x;
	o[sv_dispatchthreadid.x].w = asfloat(r0.y);
}
