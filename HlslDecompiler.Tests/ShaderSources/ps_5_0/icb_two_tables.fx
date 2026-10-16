static const int4 icb0[8] =
{
	int4(3, 0, 0, 0),
	int4(0, 0, 0, 0),
	int4(6, 0, 0, 0),
	int4(1, 0, 0, 0),
	int4(7, 0, 0, 0),
	int4(2, 0, 0, 0),
	int4(5, 0, 0, 0),
	int4(4, 0, 0, 0),
};

static const float4 icb1[8] =
{
	float4(1, 0, 0, 1),
	float4(0, 1, 0, 1),
	float4(0, 0, 1, 1),
	float4(1, 1, 0, 1),
	float4(1, 0, 1, 1),
	float4(0, 1, 1, 1),
	float4(1, 1, 1, 1),
	float4(0, 0, 0, 1),
};

float4 main(nointerpolation uint idx : IDX) : SV_Target
{
	return float4((float3)(icb0[idx + 1 & 7].x) * icb1[icb0[idx & 7].x].xyz, (float)(icb0[idx + 1 & 7].x));
}
