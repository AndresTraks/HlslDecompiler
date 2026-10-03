float4 k;

StructuredBuffer<float2> input : register(t0);
RWStructuredBuffer<uint> packed : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float t0 = input[sv_dispatchthreadid.x].y;
	int t1 = f32tof16(t0 * k.x);
	float t2 = input[sv_dispatchthreadid.x].x;
	packed[sv_dispatchthreadid.x] = 65536 * t1 + f32tof16(t2 * k.x);
}
