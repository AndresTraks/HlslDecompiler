float count;
float threshold;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = threshold - texcoord.x;
	float4 t1 = 0;
	float t2 = 0;
	for (int i = 0; i < 255; i++) {
		if (t2 >= count) {
			break;
		}
		t1 = t0 >= 0 ? t1 : t1 + texcoord;
		t2 = t2 + 1;
	}
	return t1;
}
