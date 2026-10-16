RWStructuredBuffer<uint> a : register(u0);

[numthreads(8, 1, 1)]
void main(uint sv_groupindex : SV_GroupIndex)
{
	uint t0 = a[sv_groupindex];
	if ((sv_groupindex & 1) != 0) {
		a[sv_groupindex] = 7;
	}
	a[sv_groupindex + 8] = t0;
}
