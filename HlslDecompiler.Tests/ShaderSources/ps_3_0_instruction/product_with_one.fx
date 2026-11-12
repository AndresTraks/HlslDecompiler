float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	half4 r1;
	r0.w = 1;
	r1 = tex2D(s0, texcoord.xy);
	r0.y = half(rsqrt(r1.x));
	r0.x = half(1 / r1.x);
	r0.z = r0.x * k2.x;
	o = r0 * k;

	return o;
}
