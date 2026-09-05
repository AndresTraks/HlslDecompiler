float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	float t1 = 0;
	while (true) {
		if (t1 >= 8) {
			break;
		}
		if (t1 == 3) {
			t1 = 4;
			continue;
		}
		t0 = t0 + texcoord;
		t1 = t1 + 1;
	}
	return t0;
}
