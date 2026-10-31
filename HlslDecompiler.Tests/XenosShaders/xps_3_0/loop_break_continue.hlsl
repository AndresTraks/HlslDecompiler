float4 k[8];
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = (0 < -abs(texcoord.x)) ? 1 : 0;
	float4 t1 = t0;
	float2 t2 = 0;
	float4 t3 = t0;
	for (int i = 0; i < n; i++) {
		t2.x = t2.x + 1 - (t2.x == 0 ? 1.0 : 0.0) * (k[i].w >= 0 ? 1 : 0) * (t2.x + 1);
		if ((t2.x == 0 ? 1.0 : 0.0) * (k[i].w >= 0 ? 1 : 0) != 0) {
			t1 = t1 + texcoord * k[i];
			t2.y = (10 < t1.x) ? 1 : 0;
		}
		t2.x = t2.x + 1 - (t2.x == 0 ? 1.0 : 0.0) * (t2.y == 0 ? 0.0 : 1.0) * (t2.x + 1);
		if ((t2.x == 0 ? 1.0 : 0.0) * (t2.y == 0 ? 0.0 : 1.0) != 0) {
			t2.x = 340282346638528859811704183484516925440.0;
		}
		float t4 = t2.x - 1;
		t2.x = -t4 >= 0 ? 0 : t4;
		if (t4 <= 0) {
			t3 = t1.wzyx;
		}
		float t5 = -(t2.x - 1) >= 0 ? 1 : 0;
		t2.x = -(t2.x - 1) >= 0 ? 0 : t2.x - 1;
		if (t5 != 0) {
			t3 = t1.wzyx;
		} else {
			t2.x = 340282346638528859811704183484516925440.0;
			t5 = 0;
		}
		if (t5 == 0) {
			break;
		}
	}
	return t3.wzyx;
}
