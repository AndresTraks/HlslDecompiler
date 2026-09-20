uint count;

ByteAddressBuffer source : register(t0);
RWByteAddressBuffer target : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	if (sv_dispatchthreadid.x >= count) {
		return;
	}
	uint t0 = source.Load(sv_dispatchthreadid.x * 4);
	int2 t1 = int2(t0 & 255, (77 * (t0 >> 24) + 151 * ((t0 >> 16) & 255) + 28 * ((t0 >> 8) & 255) >> 8) * 16777216);
	target.Store(sv_dispatchthreadid.x * 4, ((77 * (t0 >> 24) + 151 * ((t0 >> 16) & 255) + 28 * ((t0 >> 8) & 255) >> 8) * 65536) + t1.y + (77 * (t0 >> 24) + 151 * ((t0 >> 16) & 255) + 28 * ((t0 >> 8) & 255) & 65280) + t1.x);
}
