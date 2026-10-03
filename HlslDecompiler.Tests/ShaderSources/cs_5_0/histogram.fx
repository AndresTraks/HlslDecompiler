float4 levels;

StructuredBuffer<float> samples : register(t0);
RWByteAddressBuffer histogram : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = samples[sv_dispatchthreadid.x];
	uint t1 = min((uint)(t0 * levels.x), 15);
	histogram.InterlockedAdd(t1 * 4, 1);
	histogram.InterlockedMax(64, t1);
}
