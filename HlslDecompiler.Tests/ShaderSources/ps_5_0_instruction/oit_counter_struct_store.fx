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
	float4 o;

	int3 r0;
	int4 r1;
	int3 r2;
	r0.x = nodes.IncrementCounter();
	r0.y = ((uint)r0.x >= maxNodes) ? -1 : 0;
	if (r0.y != 0) {
		o = float4(0, 0, 0, 0);
		return o;
	}
	r1 = asint(saturate(i.color));
	r1 = asint(asfloat(r1) * float4(255, 255, 255, 255));
	r1 = (uint4)asfloat(r1);
	r0.yz = (uint2)i.sv_position.yx;
	r0.y = r0.y * width + r0.z;
	r0.y = r0.y << 2;
	heads.InterlockedExchange(r0.y, r0.x, r2.z);
	r0.y = r1.y * 256 + r1.x;
	r0.y = r1.z * 65536 + r0.y;
	r2.x = r1.w * 16777216 + r0.y;
	r0.y = countbits(i.sv_coverage.x);
	r0.y = asint((float)(uint)r0.y);
	r0.y = asint(asfloat(r0.y) * i.sv_position.z);
	r2.y = asint(asfloat(r0.y) * 0.25);
	nodes[r0.x].color = r2.x;
	nodes[r0.x].depth = asfloat(r2.y);
	nodes[r0.x].next = r2.z;
	o = float4(0, 0, 0, 0);

	return o;
}
