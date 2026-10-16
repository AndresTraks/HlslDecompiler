struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int r0;
	r0 = (i.idx == 3) ? -1 : 0;
	if (r0.x != 0) {
		o.y = 7;
	} else {
		o.y = i.texcoord.y;
	}
	r0 = (5 < i.idx) ? -1 : 0;
	if (r0.x != 0) {
		o.xzw = i.texcoord.wyw;
	} else {
		o.xz = i.texcoord.xz;
		o.w = 1;
	}

	return o;
}
