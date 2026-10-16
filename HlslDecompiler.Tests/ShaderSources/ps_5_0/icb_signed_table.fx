static const int4 icb[4] =
{
	int4(-1, 0, 0, 0),
	int4(-2, 0, 0, 0),
	int4(3, 0, 0, 0),
	int4(-2147483647, 0, 0, 0),
};

struct PS_IN
{
	nointerpolation uint idx : IDX;
	float4 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	int t0 = i.idx & 3;
	return float4((float)(icb[t0].x), (float)max(-icb[t0].x, icb[t0].x), (float)(icb[t0].x) * i.texcoord.x, (float)(icb[t0].x >> 1));
}
