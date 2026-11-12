float4 k;
float4 k2;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	float4 r1;
	r0 = tex2D(s0, texcoord.xy);
	r1 = half4(r0 * k);
	r1.xyz = r1.xyz * k2.xyz + r0.xyz;
	o = r1;

	return o;
}
