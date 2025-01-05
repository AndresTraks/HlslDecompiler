float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 3;
	for (int i = 0; i < 255; i++) {
		if (t1.x < count) {
			break;
		}
		t0 = t0 + texcoord;
		t1 = t1 + 1;
	}
	return t0;
}
