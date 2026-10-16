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
	float4 o;

	int2 r0;
	r0.x = (uint)i.idx % 3;
	r0.y = 2143289344 & icb[r0.x].x;
	r0.y = (r0.y == 2139095040) ? -1 : 0;
	r0.y = r0.y & 1;
	o.x = (float)r0.y;
	o.w = 1;
	r0.y = (asfloat(icb[r0.x].x) != asfloat(icb[r0.x].x)) ? -1 : 0;
	o.z = i.texcoord.x + asfloat(icb[r0.x].x);
	r0.x = r0.y & 1;
	o.y = (float)r0.x;

	return o;
}
