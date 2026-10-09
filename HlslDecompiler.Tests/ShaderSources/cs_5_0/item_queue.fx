float cut;

struct Item
{
	float3 p;
	uint id;
};

ConsumeStructuredBuffer<Item> inItems : register(u0);
AppendStructuredBuffer<Item> outItems : register(u1);

[numthreads(64, 1, 1)]
void main()
{
	Item t0 = inItems.Consume();
	int t1 = asint(t0.id);
	float t2 = t0.p.y;
	if (cut < t2) {
		Item t3;
		t3.p = float3(t0.p.x, t2, t0.p.z);
		t3.id = t1 + 1000;
		outItems.Append(t3);
	}
}
