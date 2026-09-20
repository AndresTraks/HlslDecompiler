StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint> bounds : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = keys[sv_dispatchthreadid.x];
	InterlockedMin(bounds[0], t0);
	InterlockedMax(bounds[1], t0);
	InterlockedXor(bounds[2], t0);
}
