cbuffer C : register(b0)
{
	int v;
	uint w;
};

RWStructuredBuffer<int> si : register(u0);
RWStructuredBuffer<uint> su : register(u1);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 t0 = sv_dispatchthreadid.x + int2(-4, 2);
	int t1;
	InterlockedMin(si[0], t0.x, t1);
	int t2;
	InterlockedMax(si[1], (int)(v - sv_dispatchthreadid.x), t2);
	int t3;
	InterlockedMin(su[0], w - sv_dispatchthreadid.x, t3);
	InterlockedMax(su[1], (uint)t0.x);
	si[t0.y] = t1 + t2 + t3;
}
