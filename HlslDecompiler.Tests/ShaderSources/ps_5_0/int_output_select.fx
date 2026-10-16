struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

uint4 main(PS_IN i) : SV_Target
{
	int t0;
	if (i.idx == 2) {
		t0 = 1075838976;
	} else {
		t0 = asint(i.texcoord.x);
	}
	return uint4(t0, i.idx > 4 ? 1065353216 : asint(i.texcoord.z), asuint(i.texcoord.z), asuint(i.texcoord.w));
}
