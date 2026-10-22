float4 levels;

StructuredBuffer<float> samples : register(t0);
RWByteAddressBuffer histogram : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	r0.x = asint(samples[sv_dispatchthreadid.x]);
	r0.x = asint(asfloat(r0.x) * levels.x);
	r0.x = (uint)asfloat(r0.x);
	r0.x = min((uint)r0.x, 15);
	r0.y = r0.x << 2;
	histogram.InterlockedAdd(r0.y, 1);
	histogram.InterlockedMax(64, (uint)r0.x);
}
