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
		float t3 = 5;
		for (int j = 0; j < 255; j++) {
			if (t3 >= count2) {
				break;
			}
			t1 = t1 + texcoord;
			t3 = t3 + 1;
		}
		t0 = t0 + texcoord;
		t2 = t2 + 1;
	}
	return t0 + t1;
}
