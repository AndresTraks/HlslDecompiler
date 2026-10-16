static const uint4 icb[4] =
{
	uint4(0x3f800000, 0x3f000000, 0x00000000, 0x00000000),
	uint4(0x00000002, 0x3fc00000, 0x00000000, 0x00000000),
	uint4(0x40400000, 0x40200000, 0x00000000, 0x00000000),
	uint4(0x00000007, 0x40600000, 0x00000000, 0x00000000),
};

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int2 r0;
	r0.x = i.idx & 3;
	r0.y = 1 & icb[r0.x].x;
	o.w = (r0.y != 0) ? 3 : asfloat(icb[r0.x].y);
	r0.y = r0.x ^ 1;
	o.y = (float)(uint)icb[r0.y].x;
	o.z = i.texcoord.x * asfloat(icb[r0.x].y);
	o.x = asfloat(icb[r0.x].x);

	return o;
}
