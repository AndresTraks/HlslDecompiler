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
	int t2 = t1;
	int t3;
	InterlockedOr(ivals[t0], 15, t3);
	int t4 = t2 + t3;
	int t5;
	InterlockedXor(ivals[t0], 170, t5);
	t4 = t4 + t5;
	int t6;
	InterlockedMax(ivals[t0], 7, t6);
	t4 = t4 + t6;
	int t7;
	InterlockedMin(ivals[t0], 3, t7);
	t4 = t4 + t7;
	int t8;
	InterlockedExchange(ivals[t0], 9, t8);
	int t9 = t8;
	int t10;
	InterlockedMax(uvals[t0], 7, t10);
	int t11 = t10;
	int t12;
	InterlockedMin(uvals[t0], 3, t12);
	int t13 = t12;
	InterlockedAnd(ivals[t0], 15);
	InterlockedOr(ivals[t0], 240);
	sink[t0] = t4 + t11 + t13 + t9;
}
