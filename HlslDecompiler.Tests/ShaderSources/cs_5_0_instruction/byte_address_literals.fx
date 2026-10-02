RWByteAddressBuffer b : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	r0 = sv_dispatchthreadid.x << 2;
	b.Store(r0.x, 7);
	b.Store2(64, int2(11, 13));
}
