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
	int2 r0;
	float4 r1;
	Item consumed0 = inItems.Consume();
	r1.x = consumed0.p.x;
	r1.y = consumed0.p.y;
	r1.z = consumed0.p.z;
	r0.x = consumed0.id;
	r0.y = (cut < r1.y) ? -1 : 0;
	if (r0.y != 0) {
		r1.w = r0.x + 1000;
		Item appended0;
		appended0.p = r1.xyz;
		appended0.id = r1.w;
		outItems.Append(appended0);
	}
}
