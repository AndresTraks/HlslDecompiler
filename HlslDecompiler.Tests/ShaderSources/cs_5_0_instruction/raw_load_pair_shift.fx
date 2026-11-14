RWByteAddressBuffer b : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	r0.xy = sv_dispatchthreadid.xx << int2(3, 2);
	r0.xz = b.Load2(r0.x);
	r0.y = r0.y + 1024;
	r0.z = r0.z << 16;
	r0.x = r0.z | r0.x;
	b.Store(r0.y, r0.x);
}
