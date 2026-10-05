cbuffer Replay : register(b0)
{
	float gain;
};

ConsumeStructuredBuffer<uint> queued : register(u0);
RWStructuredBuffer<float> replayed : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	float r0;
	uint consumed0 = queued.Consume();
	r0 = asfloat(consumed0.x);
	r0 = r0.x * gain + r0.x;
	replayed[sv_dispatchthreadid.x] = r0.x;
}
