sampler2D diffuse;
float4 wave;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	half4 r1;
	r0.xy = float2(0.15915494, 0.5);
	r0.x = half(wave.x * r0.x + r0.y);
	r0.x = half(frac(r0.x));
	r0.x = half(r0.x * 6.2831855 + -3.1415927);
	sincos(r0.x, r1.y, r1.x);
	r0 = half4(r1.yxyy * float4(1, 1, 0, 0) + float4(0, 0, 0, 1));
	r1 = tex2D(diffuse, texcoord.xy);
	o = r0 * r1;

	return o;
}
