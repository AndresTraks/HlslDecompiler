RWByteAddressBuffer b : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = sv_dispatchthreadid.x * 8;
	int2 t1 = b.Load2(t0);
	b.Store(sv_dispatchthreadid.x * 4 + 1024, t1.y | (t1.x * 65536));
}
