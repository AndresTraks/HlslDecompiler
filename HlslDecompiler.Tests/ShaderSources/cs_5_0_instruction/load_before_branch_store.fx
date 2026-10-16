RWStructuredBuffer<uint> a : register(u0);

[numthreads(8, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	int2 r0;
	r0.x = a[sv_groupindex.x];
	r0.y = sv_groupindex.x & 1;
	if (r0.y != 0) {
		a[sv_groupindex.x] = 7;
	}
	r0.y = sv_groupindex.x + 8;
	a[r0.y] = r0.x;
}
