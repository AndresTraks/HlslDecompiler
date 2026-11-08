float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.x;
	float t2 = texcoord.y;
	float t4 = texcoord.w;
	float t3 = abs(t0) >= 0 ? 0 : 1;
	float t1 = step(0, texcoord.z) * step(0, t2);
	float3 t5 = float3(1 - (0 >= 0 ? 1 : 0) * (t1 == 0 ? 0.0 : 1.0), t3, 0);
	float3 t6 = float3(t1 == 0 ? t0 : 1 + t0, 0, 0);
	if ((0 >= 0 ? 1 : 0) * (t1 == 0 ? 0.0 : 1.0) != 0) {
		t3 = texcoord.z;
		t6.z = t3;
	}
	[loop]
	for (int i = 0; i < 255; i++) {
		t5.x = t5.x + 1 - (t5.x == 0 ? 1.0 : 0.0) * (t1 == 0 ? 0.0 : 1.0) * (t5.x + 1);
		if ((t5.x == 0 ? 1.0 : 0.0) * (t1 == 0 ? 0.0 : 1.0) != 0) {
			float t7 = t4;
			float t9 = dot(float3(0.75, 0.25, 0.5), float3(t7, t0, t2));
			t0 = t0 + 0.25;
			t2 = t2 + 0.5;
			t4 = t7 + 0.75;
			float t8 = t6.z;
			t6.xz = float2(t6.x, t8) + float2(-0.5, 1);
			t3 = -t6.x >= 0 ? 0 : 1;
			t1 = t3;
			t5.y = t5.y + t9 * t8;
		}
		float t10 = -(t5.x - 1) >= 0 ? 1 : 0;
		t5.x = -(t5.x - 1) >= 0 ? 0 : t5.x - 1;
		if (t10 != 0) {
			t10 = t1 == 0 ? 0.0 : 1.0;
		}
		if (t10 == 0) {
			t5.x = 340282346638528859811704183484516925440.0;
			t10 = 0;
		}
		if (t10 == 0) {
			break;
		}
	}
	[branch]
	if (texcoord.z >= 0) {
		t6.x = 0.5;
	}
	return t5.y + t6.x;
}
