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
	int t0 = i.idx & 3;
	return float4(asfloat(icb[t0].x), (float)(icb[t0 ^ 1].x), i.texcoord.x * asfloat(icb[t0].y), 1 & icb[t0].x ? 3 : asfloat(icb[t0].y));
}
