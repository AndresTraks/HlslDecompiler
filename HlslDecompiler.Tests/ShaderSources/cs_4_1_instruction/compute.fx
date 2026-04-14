StructuredBuffer<float> InputBuffer : register(t0);
RWStructuredBuffer<float> OutputBuffer : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float r0;
	r0 = InputBuffer[sv_dispatchthreadid.x];
	r0 = r0 + r0;
	OutputBuffer[sv_dispatchthreadid.x] = r0;
}
