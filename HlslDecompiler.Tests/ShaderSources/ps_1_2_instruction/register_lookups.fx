sampler2D s0;
sampler2D s1;
sampler2D s2;
sampler2D s3;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r8;
	float2 r2;
	float4 r9;
	float4 r10;
	float4 r11;
	float4 r0;
	r8 = tex2D(s0, texcoord.xy);
	r2 = r8.wx;
	r9 = tex2D(s1, r2.xy);
	r10 = tex2D(s2, r8.xy);
	r2 = r8.yz;
	r11 = tex2D(s3, r2.xy);
	r0 = r9 * r10 + r11;
	o = r0;

	return o;
}
