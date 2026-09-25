float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	int t0 = texcoord.x > 1;
	float t1 = 0;
	int t2 = 0;
	while (t2 < 8) {
		int t3 = t0 ? 8 : t2;
		if (t1 < -100) {
			t2 = t3 + 1;
			continue;
		}
		t1 = t1 + texcoord.z;
		t2 = t3 + 1;
	}
	return t1;
}
