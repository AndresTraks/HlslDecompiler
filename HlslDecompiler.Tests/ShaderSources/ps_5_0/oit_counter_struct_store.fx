cbuffer C : register(b0)
{
	uint width;
	uint maxNodes;
};

struct Node
{
	uint color;
	float depth;
	uint next;
};

RWStructuredBuffer<Node> nodes : register(u1);
RWByteAddressBuffer heads : register(u2);

struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float4 color : COLOR;
	uint sv_coverage : SV_Coverage;
};

[earlydepthstencil]
float4 main(PS_IN i) : SV_Target
{
	uint t0 = nodes.IncrementCounter();
	if (t0 >= maxNodes) return 0;
	uint4 t1 = (uint4)(255 * saturate(i.color));
	uint t2 = (uint)i.sv_position.x;
	int t3;
	heads.InterlockedExchange(((uint)i.sv_position.y * width + t2) * 4, t0, t3);
	nodes[t0].color = 256 * t1.y + t1.x + 65536 * t1.z + 16777216 * t1.w;
	nodes[t0].depth = 0.25 * (float)countbits(i.sv_coverage) * i.sv_position.z;
	nodes[t0].next = t3;
	return 0;
}
