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
	bool t0 = a[sv_dispatchthreadid.x].x > 0.5;
	float t1 = a[sv_dispatchthreadid.x].y;
	uint t2 = (uint)(100 * a[sv_dispatchthreadid.x].z);
	o[sv_dispatchthreadid.x].bits = t0 ? asint(t1) : t2;
	o[sv_dispatchthreadid.x].w = a[sv_dispatchthreadid.x].w;
}
