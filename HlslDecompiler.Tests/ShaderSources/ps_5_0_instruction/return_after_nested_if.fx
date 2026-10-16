struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation int idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	int r0;
	r0 = (i.idx == -1) ? -1 : 0;
	if (r0.x != 0) {
		o.x = 1;
		o.yzw = i.texcoord.yzw;
		return o;
	} else {
		r0 = (i.idx == 3) ? -1 : 0;
		if (r0.x != 0) {
			o.xzw = i.texcoord.xzw;
			o.y = 2;
		} else {
			r0 = (i.idx == 4) ? -1 : 0;
			if (r0.x != 0) {
				o = float4(0, 0, 0, 0);
				return o;
			}
			o = i.texcoord.wzyx;
		}
		return o;
	}

	return o;
}
