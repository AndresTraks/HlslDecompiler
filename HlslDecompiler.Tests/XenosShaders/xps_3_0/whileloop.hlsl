float4 k;

float4 main(float4 texcoord : TEXCOORD) : COLOR
{
	float t0 = texcoord.x;
	float3 t2 = texcoord.yzw;
	float t1 = (t0 < k.x) ? 1 : 0;
	float t3 = t1;
	for (int i = 0; i < 16; i++) {
		if (t3 != 0) {
			t0 = 1.5 * t0 + 0.100000001;
			t2 = 1.5 * t2 + 0.100000001;
			float t4 = (t0 < k.x) ? 1 : 0;
			t3 = t4;
		}
		if (t3 == 0) {
			break;
		}
	}
	return float4(t0, t2);
}
