float4 k;
sampler2D s0;
sampler2D s1;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float2 r1;
	r0 = half4(tex2D(s0, texcoord.xy));
	r0.zw = ddx(texcoord.xy);
	r1 = ddy(texcoord.xy);
	r0 = tex2Dgrad(s1, r0.xy, r0.zw, r1.xy);
	o = r0 * k;

	return o;
}
