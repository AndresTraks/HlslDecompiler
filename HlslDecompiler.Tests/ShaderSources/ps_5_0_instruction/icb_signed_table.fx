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
	float4 o;

	int2 r0;
	r0.x = i.idx & 3;
	r0.y = icb[r0.x].x >> 1;
	o.w = (float)r0.y;
	r0.y = max(-(icb[r0.x].x), icb[r0.x].x);
	r0.x = asint((float)icb[r0.x].x);
	o.y = (float)r0.y;
	o.z = asfloat(r0.x) * i.texcoord.x;
	o.x = asfloat(r0.x);

	return o;
}
