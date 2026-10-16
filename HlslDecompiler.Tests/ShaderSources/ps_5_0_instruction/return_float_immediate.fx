struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	float4 o;

	float4 r0;
	int4 r1;
	float4 r2;
	if (i.idx == 0) {
		o = i.texcoord;
		return o;
	}
	r0.x = (i.idx == 1) ? -1 : 0;
	if (asint(r0.x) != 0) {
		r0 = i.texcoord.xzwy;
	} else {
		r1.x = (i.idx == 2) ? -1 : 0;
		if (r1.x != 0) {
			o = float4(1, 1, 1, 1);
			return o;
		}
		r0 = -(i.texcoord.yzwx);
	}
	r1.x = i.idx & 3;
	r2 = r0.wxyz;
	r1.y = 0;
	while (true) {
		r1.z = ((uint)r1.y >= (uint)r1.x) ? -1 : 0;
		if (r1.z != 0) break;
		r1.z = asint((float)(uint)r1.y);
		r1.w = (asfloat(r1.z) < r2.x) ? -1 : 0;
		if (r1.w != 0) {
			o = asfloat(r1.z) * r2;
			return o;
		}
		r2 = r2 + float4(1, 1, 1, 1);
		r1.y = r1.y + 1;
	}
	o = r2;

	return o;
}
