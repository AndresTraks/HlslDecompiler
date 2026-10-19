struct S
{
	float w[4];
	uint m[2];
};

StructuredBuffer<S> a : register(t0);
RWStructuredBuffer<S> o : register(u0);

[numthreads(16, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.xy = (int2(0, 0) & ~(((1 << int2(2, 1)) - 1) << int2(2, 2))) | ((sv_dispatchthreadid.yy << int2(2, 2)) & (((1 << int2(2, 1)) - 1) << int2(2, 2)));
	r0.y = r0.y + 16;
	r0.y = a[sv_dispatchthreadid.x].m[(r0.y - 16) / 4];
	r0.y = asint((float)(uint)r0.y);
	r0.z = -(r0.x) + 12;
	r0.z = asint(a[sv_dispatchthreadid.x].w[r0.z / 4]);
	r0.y = asint(asfloat(r0.y) + asfloat(r0.z));
	o[sv_dispatchthreadid.x].w[r0.x / 4] = asfloat(r0.y);
}
