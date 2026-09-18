StructuredBuffer<uint> source : register(t0);
RWStructuredBuffer<float2> unpacked : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int2 r0;
	float2 r1;
	r0.x = source[sv_dispatchthreadid.x];
	r0.y = (uint)r0.x >> 16;
	r1 = f16tof32(r0.xy);
	unpacked[sv_dispatchthreadid.x] = r1.xy;
}
