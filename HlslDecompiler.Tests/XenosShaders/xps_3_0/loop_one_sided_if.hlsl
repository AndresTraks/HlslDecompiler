float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.x;
	float t1 = texcoord.y;
	float t5 = texcoord.w;
	float t2 = abs(t0) >= 0 ? 0 : 1;
	float t4 = 1 + t0;
	float t3 = step(0, texcoord.z) * step(0, t1);
	float2 t6 = float2(t2, 0);
	float3 t7 = float3(t3 == 0 ? t0 : t4, 0, 0);
	float2 t8 = float2(t3 == 0 ? 0.0 : 1.0, 0);
	if (t8.x != 0) {
		t2 = texcoord.z;
		t7.z = t2;
	}
	[loop]
	for (int i = 0; i < 255; i++) {
		float t9 = t3 == 0 ? 0.0 : 1.0;
		if (t8.x != 0) {
			if (t9 != 0) {
				t7.xy = t7.xz + float2(-0.5, 0);
				t7.z = t7.y + 1;
				t2 = -t7.x >= 0 ? 0 : 1;
				t3 = t2;
				t6.y = t5;
				t4 = dot(float3(0.75, 0.25, 0.5), float3(t6.y, t0, t1));
				t6.x = t6.x + t4 * t7.y;
				t0 = t0 + 0.25;
				t1 = t1 + 0.5;
				t5 = t6.y + 0.75;
			}
			t8.y = t3 == 0 ? 0.0 : 1.0;
		}
		if (t8.x != 0) {
			t8.x = t8.y;
		}
		if (t8.x == 0) {
			break;
		}
	}
	return t6.x + (texcoord.z >= 0 ? 0.5 : t7.x);
}
