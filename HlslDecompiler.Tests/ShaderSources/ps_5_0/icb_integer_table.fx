static const int4 icb[4] =
{
	int4(1065353216, 1056964608, 0, 0),
	int4(2, 1069547520, 0, 0),
	int4(1077936128, 1075838976, 0, 0),
	int4(7, 1080033280, 0, 0),
};

struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = i.idx & 3;
	return float4(asfloat(icb[t0].x), (float)(uint)(icb[t0 ^ 1].x), i.texcoord.x * asfloat(icb[t0].y), 1 & icb[t0].x ? 3 : asfloat(icb[t0].y));
}
