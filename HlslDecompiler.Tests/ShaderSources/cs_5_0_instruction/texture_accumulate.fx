RWTexture2D<float4> accumulator : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float4 r0;
	r0 = accumulator[sv_dispatchthreadid.xy];
	r0 = r0 * float4(0.5, 0.5, 0.5, 0.5) + float4(0.25, 0.25, 0.25, 0.25);
	accumulator[sv_dispatchthreadid.xy] = r0;
}
