int count : register(c1);
float4 step;
sampler2D t;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 t0 = 0;
	float t1 = 0;
	for (int i = 0; i < count; i++) {
		float4 t2 = tex2Dlod(t, float4(step.xy * t1 + texcoord, 0, 0));
		t0 = t0 + t2;
		t1 = t1 + 1;
	}
	return t0 / count;
}
