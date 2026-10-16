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
	float4 o;

	int2 r0;
	r0.x = (uint)i.idx % 3;
	r0.x = r0.x << 2;
	r0.y = i.idx & icb[r0.x + 1].x;
	o.y = (float)(uint)r0.y;
	o.x = i.texcoord.x * asfloat(icb[r0.x].x);
	o.z = (float)icb[r0.x + 2].x;
	o.w = i.texcoord.y + asfloat(icb[r0.x + 3].x);

	return o;
}
