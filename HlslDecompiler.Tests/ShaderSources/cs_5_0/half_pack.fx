float4 k;

StructuredBuffer<float2> input : register(t0);
RWStructuredBuffer<uint> packed : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int t0 = f32tof16(input[sv_dispatchthreadid.x].y * k.x);
	packed[sv_dispatchthreadid.x] = 65536 * t0 + f32tof16(input[sv_dispatchthreadid.x].x * k.x);
}
