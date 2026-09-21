StructuredBuffer<uint> keys : register(t0);
RWStructuredBuffer<uint> bounds : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	r0 = keys[sv_dispatchthreadid.x];
	InterlockedMin(bounds[0], (uint)r0.x);
	InterlockedMax(bounds[1], (uint)r0.x);
	InterlockedXor(bounds[2], r0.x);
}
