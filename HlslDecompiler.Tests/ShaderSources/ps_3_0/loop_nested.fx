float count;
float count2;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float4 t1 = 0;
	float t2 = 3;
	for (int i = 0; i < 255; i++) {
		if (t2 >= count) {
			break;
		}
		float4 t3 = t1;
		float t4 = 5;
		for (int i = 0; i < 255; i++) {
			if (t4 >= count2) {
				break;
			}
			t3 = t3 + texcoord;
			t4 = t4 + 1;
		}
		t0 = t0 + texcoord;
		t1 = t3;
		t2 = t2 + 1;
	}
	return t0 + t1;
}
