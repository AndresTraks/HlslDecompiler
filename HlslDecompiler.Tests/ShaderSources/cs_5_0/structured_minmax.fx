StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint> bounds : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	InterlockedMin(bounds[0], keys[sv_dispatchthreadid.x]);
	InterlockedMax(bounds[1], keys[sv_dispatchthreadid.x]);
	InterlockedXor(bounds[2], keys[sv_dispatchthreadid.x]);
}
