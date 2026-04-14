StructuredBuffer<float> InputBuffer : register(t0);
RWStructuredBuffer<float> OutputBuffer : register(u0);

[numthreads(256, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float value = InputBuffer[sv_dispatchthreadid.x];
	OutputBuffer[sv_dispatchthreadid.x] = value * 2.0f;
}
