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
	float4 o;

	int2 r0;
	r0.x = i.idx & 3;
	r0.y = 1 & icb[r0.x].x;
	o.w = (r0.y != 0) ? 3 : asfloat(icb[r0.x].y);
	r0.y = r0.x ^ 1;
	o.y = (float)(uint)icb[r0.y].x;
	o.z = i.texcoord.x * asfloat(icb[r0.x].y);
	o.x = asfloat(icb[r0.x].x);

	return o;
}
