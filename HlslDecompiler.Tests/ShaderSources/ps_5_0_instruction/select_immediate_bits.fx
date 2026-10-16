struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int2 r0;
	o.w = 1;
	r0.x = (i.idx == 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? 1088421888 : asint(i.texcoord.y);
	r0.y = (uint)r0.x >> 30;
	o.y = (float)(uint)r0.y;
	o.z = asfloat(r0.x ^ -2147483648);
	o.x = asfloat(r0.x);

	return o;
}
