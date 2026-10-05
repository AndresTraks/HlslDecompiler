ConsumeStructuredBuffer<uint> queued : register(u0);
RWStructuredBuffer<uint> totals : register(u1);

[numthreads(64, 1, 1)]
void main()
{
	uint t0 = queued.Consume();
	InterlockedMax(totals[0], t0);
}
