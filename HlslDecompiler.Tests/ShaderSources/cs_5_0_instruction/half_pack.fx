float4 k;

StructuredBuffer<float2> input : register(t0);
RWStructuredBuffer<uint> packed : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float2 r0;
	r0 = input[sv_dispatchthreadid.x];
	r0 = r0.xy * k.xx;
	r0 = f32tof16(r0.xy);
	r0.x = r0.y * 65536 + r0.x;
	packed[sv_dispatchthreadid.x] = r0.x;
}
