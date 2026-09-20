RWBuffer<float4> destination : register(u0);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float r0;
	r0 = (float)(uint)sv_dispatchthreadid.x;
	r0 = r0.x * 0.25;
	destination[sv_dispatchthreadid.x] = r0.x;
}
