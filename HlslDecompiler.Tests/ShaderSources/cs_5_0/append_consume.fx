AppendStructuredBuffer<uint> app : register(u0);
ConsumeStructuredBuffer<uint> con : register(u1);

[numthreads(8, 1, 1)]
void main()
{
	uint t0 = con.Consume();
	app.Append(t0 * 2);
}
