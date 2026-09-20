RWBuffer<float4> destination : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	destination[sv_dispatchthreadid.x] = 0.25 * (float4)sv_dispatchthreadid.x;
}
