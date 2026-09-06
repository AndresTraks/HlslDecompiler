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
	float4 o;

	int a0;
	float3 r0;
	a0 = idx.x;
	r0 = float3(1, 0, 0);
	o = c1[a0].xyzx * r0.xxxy + r0.yyzx;

	return o;
}
