uint count;

ByteAddressBuffer source : register(t0);
RWByteAddressBuffer target : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int4 r0;
	int r1;
	int3 r2;
	r0.x = (sv_dispatchthreadid.x >= count) ? -1 : 0;
	if (r0.x != 0) {
		return;
	}
	r0.x = sv_dispatchthreadid.x << 2;
	r1 = source.Load(r0.x);
	r2.x = (uint)r1.x >> 24;
	r2.y = (uint)r1.x >> 16;
	r2.z = (uint)r1.x >> 8;
	r0.yz = r2.yz & int2(255, 255);
	r0.y = r0.y * 151;
	r0.y = r2.x * 77 + r0.y;
	r0.y = r0.z * 28 + r0.y;
	r0.z = (uint)r0.y >> 8;
	r0.w = r0.z << 24;
	r0.z = r0.z << 16;
	r0.z = r0.z + r0.w;
	r0.y = r0.y & 65280;
	r0.y = r0.y + r0.z;
	r0.z = r1.x & 255;
	r0.y = r0.z + r0.y;
	target.Store(r0.x, r0.y);
}
