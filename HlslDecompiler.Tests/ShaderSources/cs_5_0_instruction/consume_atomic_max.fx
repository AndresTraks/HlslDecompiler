ConsumeStructuredBuffer<uint> queued : register(u0);
RWStructuredBuffer<uint> totals : register(u1);

[numthreads(64, 1, 1)]
void main()
{
	int r0;
	uint consumed0 = queued.Consume();
	r0 = consumed0.x;
	InterlockedMax(totals[0], (uint)r0.x);
}
