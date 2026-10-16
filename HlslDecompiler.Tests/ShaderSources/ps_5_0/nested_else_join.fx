struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint idx : IDX;
};

float4 main(PS_IN i) : SV_Target
{
	uint t0 = i.idx & 3;
	float4 t1;
	if (t0 == 0) {
		t1 = i.texcoord;
	} else {
		if (t0 == 1) {
			t1 = i.texcoord.yzwx;
		} else {
			t1 = -i.texcoord;
		}
	}
	for (uint t2 = 0; t2 < t0; t2 = t2 + 1) {
		t1 = t1 + 1;
	}
	return t1;
}
