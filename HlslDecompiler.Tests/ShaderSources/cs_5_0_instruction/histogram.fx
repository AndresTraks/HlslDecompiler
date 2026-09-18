float4 levels;

StructuredBuffer<float> samples : register(t0);
RWByteAddressBuffer histogram : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	r0.x = samples[sv_dispatchthreadid.x];
	r0.x = r0.x * levels.x;
	r0.x = (uint)r0.x;
	r0.x = min(r0.x, 15);
	r0.y = (int)r0.x << 2;
	histogram.InterlockedAdd(r0.y, 1);
	histogram.InterlockedMax(64, r0.x);
}
