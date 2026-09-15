float count;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float4 t1 = 1;
	float t2 = 0;
	for (int i = 0; i < 255; i++) {
		if (t2 >= count) {
			break;
		}
		t0 = t0 + texcoord;
		t1 = t1 * texcoord;
		t2 = t2 + 1;
	}
	return t0 + t1;
}
