sampler2D s0;
float4 steps[16];
float threshold;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < 16; i++) {
		float4 t2 = tex2Dlod(s0, float4(steps[t1].xy + texcoord, 0, 0));
		if (threshold < t2.w) {
			break;
		}
		t0 = t0 + t2;
		t1 = t1 + 1;
	}
	return t0;
}
