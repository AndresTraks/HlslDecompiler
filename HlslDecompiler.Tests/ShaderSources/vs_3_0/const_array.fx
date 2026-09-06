int idx;

static const float4 c1[4] =
{
	float4(1, 0, 0, 0),
	float4(0, 1, 0, 0),
	float4(0, 0, 1, 0),
	float4(1, 1, 0, 0),
};

float4 main() : POSITION
{
	return float4(c1[idx].xyz, 1);
}
