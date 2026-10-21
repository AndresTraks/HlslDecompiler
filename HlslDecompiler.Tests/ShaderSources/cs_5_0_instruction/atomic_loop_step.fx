RWStructuredBuffer<uint> lock : register(u0);
RWStructuredBuffer<uint> o : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	r0.xy = int2(0, 0);
	while (true) {
		r0.z = ((uint)r0.y >= 4) ? -1 : 0;
		if (r0.z != 0) break;
		r0.z = r0.y + 1;
		InterlockedCompareExchange(lock[0], r0.y, r0.z, r1);
		r0.w = (r0.y == r1.x) ? -1 : 0;
		if (r0.w != 0) {
			r0.x = r1.x;
			break;
		}
		r0.y = r0.z;
		r0.x = r1.x;
	}
	r0.x = r0.x * 3;
	o[sv_dispatchthreadid.x] = r0.x;
}
