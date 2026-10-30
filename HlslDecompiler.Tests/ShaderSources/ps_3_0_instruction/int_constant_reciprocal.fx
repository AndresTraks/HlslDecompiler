int count : register(c1);
float4 step;
sampler2D t;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float r2;
	float4 r3;
	r0.zw = 0;
	r1 = 0;
	r2 = 0;
	for (int i0 = 0; i0 < count; i0++) {
		r0.xy = step.xy * r2.xx + texcoord.xy;
		r3 = tex2Dlod(t, r0);
		r1 = r1 + r3;
		r2 = r2.x + 1;
	}
	r0.x = 1.0 / count.x;
	o = r0.x * r1;

	return o;
}
