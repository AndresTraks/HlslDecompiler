float4 levels;

StructuredBuffer<float> samples : register(t0);
RWByteAddressBuffer histogram : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = min((uint)(samples[sv_dispatchthreadid.x] * levels.x), 15);
	histogram.InterlockedAdd(t0 * 4, 1);
	histogram.InterlockedMax(64, t0);
}
