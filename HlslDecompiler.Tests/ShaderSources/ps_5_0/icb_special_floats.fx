static const int4 icb[3] =
{
	int4(2139095040, 0, 0, 0),
	int4(-8388608, 0, 0, 0),
	int4(2143289344, 0, 0, 0),
};

struct PS_IN
{
	nointerpolation uint idx : IDX;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	return float4((2143289344 & icb[i.idx % 3].x) == 2139095040, isnan(asfloat(icb[i.idx % 3].x)) ? 1.0 : 0.0, i.texcoord.x + asfloat(icb[i.idx % 3].x), 1);
}
