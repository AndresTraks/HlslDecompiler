RWByteAddressBuffer b : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	b.Store(sv_dispatchthreadid.x * 4, 7);
	b.Store2(64, int2(11, 13));
}
