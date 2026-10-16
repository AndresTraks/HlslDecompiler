static const int4 icb[12] =
{
	int4(1056964608, 0, 0, 0),
	int4(3, 0, 0, 0),
	int4(-1, 0, 0, 0),
	int4(1073741824, 0, 0, 0),
	int4(1048576000, 0, 0, 0),
	int4(12, 0, 0, 0),
	int4(1, 0, 0, 0),
	int4(1082130432, 0, 0, 0),
	int4(1040187392, 0, 0, 0),
	int4(48, 0, 0, 0),
	int4(-2, 0, 0, 0),
	int4(1090519040, 0, 0, 0),
};

struct PS_IN
{
	nointerpolation uint idx : IDX;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = i.idx % 3 * 4;
	return float4(i.texcoord.x * asfloat(icb[t0].x), (float)(i.idx & icb[t0 + 1].x), (float)(icb[t0 + 2].x), i.texcoord.y + asfloat(icb[t0 + 3].x));
}
