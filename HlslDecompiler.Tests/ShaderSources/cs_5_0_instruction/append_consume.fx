AppendStructuredBuffer<uint> app : register(u0);
ConsumeStructuredBuffer<uint> con : register(u1);

[numthreads(8, 1, 1)]
void main()
{
	int r0;
	uint consumed0 = con.Consume();
	r0 = consumed0.x;
	r0 = r0.x << 1;
	app.Append(r0.x);
}
