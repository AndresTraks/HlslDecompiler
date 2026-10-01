StructuredBuffer<int> idx : register(t0);
RWStructuredBuffer<int> ivals : register(u0);
RWStructuredBuffer<uint> uvals : register(u1);
RWStructuredBuffer<int> sink : register(u2);

[numthreads(8, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int3 r0;
	int r1;
	int r2;
	int r3;
	r0.y = 0;
	r0.x = idx[sv_dispatchthreadid.x];
	InterlockedAnd(ivals[r0.x], 240, r1);
	InterlockedOr(ivals[r0.x], 15, r2);
	r0.z = r1.x + r2.x;
	InterlockedXor(ivals[r0.x], 170, r1);
	r0.z = r0.z + r1.x;
	InterlockedMax(ivals[r0.x], 7, r1);
	r0.z = r0.z + r1.x;
	InterlockedMin(ivals[r0.x], 3, r1);
	r0.z = r0.z + r1.x;
	InterlockedExchange(ivals[r0.x], 9, r1);
	InterlockedMax(uvals[r0.x], (uint)7, r2);
	InterlockedMin(uvals[r0.x], (uint)3, r3);
	r0.z = r0.z + r2.x;
	r0.z = r3.x + r0.z;
	r0.z = r1.x + r0.z;
	InterlockedAnd(ivals[r0.x], 15);
	InterlockedOr(ivals[r0.x], 240);
	sink[r0.x] = r0.z;
}
