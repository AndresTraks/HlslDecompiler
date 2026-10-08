StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<int> ivals : register(u0);
RWStructuredBuffer<uint> uvals : register(u1);
RWStructuredBuffer<int> sink : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = idx[sv_dispatchthreadid.x];
	int t1;
	InterlockedAnd(ivals[t0], 240, t1);
	int t2;
	InterlockedOr(ivals[t0], 15, t2);
	int t3 = t1 + t2;
	int t4;
	InterlockedXor(ivals[t0], 170, t4);
	t3 = t3 + t4;
	int t5;
	InterlockedMax(ivals[t0], 7, t5);
	t3 = t3 + t5;
	int t6;
	InterlockedMin(ivals[t0], 3, t6);
	t3 = t3 + t6;
	int t7;
	InterlockedExchange(ivals[t0], 9, t7);
	int t8;
	InterlockedMax(uvals[t0], 7, t8);
	int t9;
	InterlockedMin(uvals[t0], 3, t9);
	InterlockedAnd(ivals[t0], 15);
	InterlockedOr(ivals[t0], 240);
	sink[t0] = t3 + t8 + t9 + t7;
}
