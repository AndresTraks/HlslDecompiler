float4 k[8];
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float4 t1 = 0;
	float t2 = 0;
	float t3 = 0;
	[loop]
	for (int i = 0; i < n; i++) {
		if (k[i].w >= 0) {
			t0 = t0 + texcoord * k[i];
			float t4 = (10 < t0.x) ? 1 : 0;
			float t5 = t4 == 0 ? 0.0 : 1.0;
			if (t5 != 0) {
				break;
			}
		}
		t1 = t0.wzyx;
	}
	return t1.wzyx;
}
