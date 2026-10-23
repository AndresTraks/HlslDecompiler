StructuredBuffer<float> v : register(t0);
RWStructuredBuffer<uint> m : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int2 r1;
	r0.x = asint(v[sv_dispatchthreadid.x]);
	r0.y = m[0];
	r0.z = r0.y;
	r0.w = 0;
	while (true) {
		r1.x = (r0.w >= 16) ? -1 : 0;
		if (r1.x != 0) break;
		r1.x = (asfloat(r0.z) >= asfloat(r0.x)) ? -1 : 0;
		if (r1.x != 0) {
			break;
		}
		InterlockedCompareExchange(m[0], r0.z, r0.x, r1.x);
		r1.y = (r0.z == r1.x) ? -1 : 0;
		if (r1.y != 0) {
			break;
		}
		r0.z = r1.x;
		r0.w = r0.w + 1;
	}
}
