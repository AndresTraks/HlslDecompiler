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
	float4 o;

	int2 r0;
	float4 r1;
	r0.x = idx + 1;
	r0.x = r0.x & 7;
	r0.x = asint((float)icb0[r0.x].x);
	r0.y = idx & 7;
	r0.y = icb0[r0.y].x;
	r1.xyz = icb1[r0.y].xyz;
	r1.w = 1;
	o = asfloat(r0.x) * r1;

	return o;
}
