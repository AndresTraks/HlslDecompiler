sampler2D s0;
float4 steps[8];

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 8; i++) {
		float2 t2 = float2(steps[t1].z, t1 + 1);
		if (t2.x >= 0) {
			t2.x = t2.x >= 0 ? t1 : t2.y;
			t0 = t0 + tex2Dlod(s0, float4(steps[t2.x].xy + texcoord, 0, 0)) * steps[t2.x].z;
		}
		t1 = t2.y;
	}
	return t0;
}
