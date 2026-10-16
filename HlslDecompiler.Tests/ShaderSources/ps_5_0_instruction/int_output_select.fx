struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

uint4 main(PS_IN i) : SV_Target
{
	uint4 o;

	int4 r0;
	int r1;
	r0.x = (i.idx == 2) ? -1 : 0;
	if (r0.x != 0) {
		r0.x = 1075838976;
	} else {
		r0.x = asint(i.texcoord.x);
	}
	r1 = (4 < i.idx) ? -1 : 0;
	r0.y = (r1.x != 0) ? 1065353216 : asint(i.texcoord.z);
	r0.zw = asint(i.texcoord.zw);
	o = r0;

	return o;
}
