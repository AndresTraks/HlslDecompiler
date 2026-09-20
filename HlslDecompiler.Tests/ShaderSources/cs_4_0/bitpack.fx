uint count;

ByteAddressBuffer source : register(t0);
RWByteAddressBuffer target : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	if (sv_dispatchthreadid.x >= count) {
		return;
	}
	int t0 = sv_dispatchthreadid.x * 4;
	uint t1 = source.Load(t0);
	int2 t2 = int2(t1 & 255, (77 * (t1 >> 24) + 151 * ((t1 >> 16) & 255) + 28 * ((t1 >> 8) & 255) >> 8) * 16777216);
	uint t3 = 77 * (t1 >> 24) + 151 * ((t1 >> 16) & 255) + 28 * ((t1 >> 8) & 255);
	target.Store(t0, ((t3 >> 8) * 65536) + t2.y + (t3 & 65280) + t2.x);
}
