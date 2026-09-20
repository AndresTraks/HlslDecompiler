StructuredBuffer<uint> source : register(t0);
RWStructuredBuffer<float2> unpacked : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = source[sv_dispatchthreadid.x];
	unpacked[sv_dispatchthreadid.x] = float2(f16tof32(t0), f16tof32(t0 >> 16));
}
