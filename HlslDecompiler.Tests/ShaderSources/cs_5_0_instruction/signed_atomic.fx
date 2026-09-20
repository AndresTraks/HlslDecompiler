StructuredBuffer<int> values : register(t0);
RWStructuredBuffer<int> bounds : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	r0 = values[sv_dispatchthreadid.x];
	InterlockedMin(bounds[0], r0.x);
	InterlockedMax(bounds[1], r0.x);
}
