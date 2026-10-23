cbuffer C : register(b0)
{
	uint w;
	uint w2;
};

RWStructuredBuffer<int> si : register(u0);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0;
	InterlockedMax(si[sv_dispatchthreadid.x & 1], (int)w, t0);
	InterlockedMin(si[(2 & ~1) | (sv_dispatchthreadid.x & 1)], (int)w2);
	si[sv_dispatchthreadid.x + 4] = t0;
}
