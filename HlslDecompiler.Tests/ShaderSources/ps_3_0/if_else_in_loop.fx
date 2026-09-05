float count;
float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 255; i++) {
		if (t1 >= count) {
			break;
		}
		if (threshold < texcoord.x) {
			t0 = t0 + texcoord;
		} else {
			t0 = 0.5 * t0;
		}
		t1 = t1 + 1;
	}
	return t0;
}
