uint4 k;

float4 main(float4 texcoord : TEXCOORD) : SV_Target
{
	float4 t0 = 0;
	for (uint t1 = 0; t1 < k.x; t1 = t1 + 1) {
		float4 t2;
		uint t3;
		switch (t1 & 1) {
			case 0:
				t2 = t0 + texcoord;
				break;
			default:
				t2 = t0;
				for (t3 = 0; t3 < k.y; t3 = t3 + 1) {
					t2 = t2 + texcoord * (float4)t3;
				}
				break;
		}
		if (t2.y > 50) {
			t0 = t2;
			break;
		}
		t0 = t2;
	}
	return t0;
}
