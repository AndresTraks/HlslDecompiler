struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	if (i.idx == 0) return i.texcoord;
	float4 t0;
	if (i.idx == 1) {
		t0 = i.texcoord.xzwy;
	} else {
		if (i.idx == 2) return 1;
		t0 = -i.texcoord.yzwx;
	}
	uint t1 = i.idx & 3;
	for (uint t2 = 0; t2 < t1; t2 = t2 + 1) {
		float t3 = (float)t2;
		if (t3 < t0.w) return t0.wxyz * t3;
		t0.wxyz = t0.wxyz + 1;
	}
	return t0.wxyz;
}
