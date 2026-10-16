RWByteAddressBuffer b : register(u0);

[numthreads(8, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	uint t0 = sv_groupindex * 4;
	int t1 = b.Load(t0);
	b.Store(t0, t1 * 2);
	b.Store(t0 + 64, 3 * t1);
}
