cbuffer Replay : register(b0)
{
	float gain;
};

ConsumeStructuredBuffer<uint> queued : register(u0);
RWStructuredBuffer<float> replayed : register(u1);

[numthreads(64, 1, 1)]
void main(uint3 sv_dispatchthreadid : SV_DispatchThreadID)
{
	int r0;
	uint consumed0 = queued.Consume();
	r0 = consumed0.x;
	r0 = asint(asfloat(r0.x) * gain + asfloat(r0.x));
	replayed[sv_dispatchthreadid.x] = asfloat(r0.x);
}
