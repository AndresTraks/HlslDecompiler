RWStructuredBuffer<uint> lock : register(u0);
RWStructuredBuffer<uint> o : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 t0 = 0;
	while (t0.y < 4) {
		uint t1 = t0.y + 1;
		int t2;
		InterlockedCompareExchange(lock[0], t0.y, t1, t2);
		if (t0.y == t2) {
			t0.x = t2;
			break;
		}
		t0 = int2(t2, t1);
	}
	o[sv_dispatchthreadid.x] = 3 * t0.x;
}
