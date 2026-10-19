struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int2 r0;
	r0.x = (i.idx == 3) ? -1 : 0;
	r0.x = (r0.x != 0) ? 1088421888 : asint(i.texcoord.y);
	r0.y = (5 < i.idx) ? -1 : 0;
	if (r0.y != 0) {
		o.xzw = i.texcoord.wyw;
		o.y = asfloat(r0.x);
		return o;
	} else {
		o.xz = i.texcoord.xz;
		o.y = asfloat(r0.x);
		o.w = 1;
		return o;
	}

	return o;
}
