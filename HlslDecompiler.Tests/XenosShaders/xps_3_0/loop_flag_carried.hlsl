float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t2 = texcoord.x;
	float t0 = texcoord.y;
	float2 t1 = float2(t0 >= 0 ? 1 : 0, 0);
	float2 t3 = texcoord.zw;
	float2 t4 = float2(t1.x, 0);
	float2 t5 = 0;
	float2 t6 = float2(t1.x, 0);
	[loop]
	for (int i = 0; i < 255; i++) {
		float t7 = -t3.x >= 0 ? 0 : 1;
		if (t4.x != 0) {
			if (t7 != 0) {
				t2 = t2 + 0.25;
				t0 = t0 + 0.5;
				t3 = t3 + float2(-1, -0.75);
			}
			t6.x = -t3.y >= 0 ? 0 : 1;
			t4.y = -t3.y >= 0 ? 0 : 1;
		}
		if (t4.x != 0) {
			t4.x = t4.y;
		}
		if (t4.x == 0) {
			break;
		}
	}
	float t8 = t6.x == 0 ? 1.0 : 0.0;
	[loop]
	for (int j = 0; j < 255; j++) {
		if (t8 != 0) {
			float t9 = (2 < t2) ? 1 : 0;
			float t10 = t9 == 0 ? 0.0 : 1.0;
			if (t10 != 0) {
				t2 = t2 - 0.5;
				t0 = t0 - 0.25;
				t3 = t3 - float2(0.75, 1);
				t6.x = -t0 >= 0 ? 0 : 1;
			}
			t5.y = t6.x == 0 ? 1.0 : 0.0;
		}
		if (t8 != 0) {
			t8 = t5.y;
		}
		if (t8 == 0) {
			break;
		}
	}
	return float4(t2, t0, t3) + t6.x;
}
