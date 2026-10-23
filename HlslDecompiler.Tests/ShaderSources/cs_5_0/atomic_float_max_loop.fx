StructuredBuffer<float> v : register(t0);
RWStructuredBuffer<uint> m : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = v[sv_dispatchthreadid.x];
	int t1 = m[0];
	for (int t2 = 0; t2 < 16; t2 = t2 + 1) {
		if (asfloat(t1) >= t0) {
			break;
		}
		int t3;
		InterlockedCompareExchange(m[0], t1, asint(t0), t3);
		if (t1 == t3) {
			break;
		}
		t1 = t3;
	}
}
