cbuffer Replay : register(b0)
{
	float gain;
};

ConsumeStructuredBuffer<uint> queued : register(u0);
RWStructuredBuffer<float> replayed : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	uint t0 = queued.Consume();
	replayed[sv_dispatchthreadid.x] = asfloat(t0) * gain + asfloat(t0);
}
