float4 k[8];
int n : register(i0);

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float2 t1 = 0;
	float4 t2 = 0;
	[loop]
	for (int i = 0; i < n; i++) {
		t1.x = t1.x + 1 - (t1.x == 0 ? 1.0 : 0.0) * (k[i].w >= 0 ? 1 : 0) * (t1.x + 1);
		if ((t1.x == 0 ? 1.0 : 0.0) * (k[i].w >= 0 ? 1 : 0) != 0) {
			t0 = t0 + texcoord * k[i];
			t1.y = (10 < t0.x) ? 1 : 0;
		}
		t1.x = t1.x + 1 - (t1.x == 0 ? 1.0 : 0.0) * (t1.y == 0 ? 0.0 : 1.0) * (t1.x + 1);
		if ((t1.x == 0 ? 1.0 : 0.0) * (t1.y == 0 ? 0.0 : 1.0) != 0) {
			t1.x = 340282346638528859811704183484516925440.0;
		}
		float t3 = t1.x - 1;
		t1.x = -t3 >= 0 ? 0 : t3;
		if (t3 <= 0) {
			t2 = t0.wzyx;
		}
		float t4 = -(t1.x - 1) >= 0 ? 1 : 0;
		t1.x = -(t1.x - 1) >= 0 ? 0 : t1.x - 1;
		if (t4 != 0) {
			t2 = t0.wzyx;
		} else {
			t1.x = 340282346638528859811704183484516925440.0;
			t4 = 0;
		}
		if (t4 == 0) {
			break;
		}
	}
	return t2.wzyx;
}
