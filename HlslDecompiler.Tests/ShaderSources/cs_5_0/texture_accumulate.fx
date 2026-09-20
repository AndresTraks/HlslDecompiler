RWTexture2D<float4> accumulator : register(u0);

[numthreads(8, 8, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	accumulator[sv_dispatchthreadid.xy] = 0.5 * accumulator[sv_dispatchthreadid.xy] + 0.25;
}
