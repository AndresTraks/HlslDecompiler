RWByteAddressBuffer b : register(u0);

[numthreads(8, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	int3 r0;
	r0.x = sv_groupindex.x << 2;
	r0.y = b.Load(r0.x);
	r0.z = r0.y << 1;
	r0.y = r0.y * 3;
	b.Store(r0.x, r0.z);
	r0.x = r0.x + 64;
	b.Store(r0.x, r0.y);
}
