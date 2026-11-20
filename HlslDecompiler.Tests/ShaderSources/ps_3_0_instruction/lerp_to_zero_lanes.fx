float blend;
sampler2D detail;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float3 r1;
	r0 = tex2D(detail, texcoord.xy);
	r0.xyz = half3(r0.xyz * 4 + -2);
	r1 = -r0.xyz + float3(1, 0, 0);
	o.xyz = half3(blend.xxx * r1.xyz + r0.xyz);
	o.w = 0.5;

	return o;
}
