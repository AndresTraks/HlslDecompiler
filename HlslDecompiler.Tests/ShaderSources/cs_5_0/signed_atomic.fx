StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> bounds : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	InterlockedMin(bounds[0], values[sv_dispatchthreadid.x]);
	InterlockedMax(bounds[1], values[sv_dispatchthreadid.x]);
}
