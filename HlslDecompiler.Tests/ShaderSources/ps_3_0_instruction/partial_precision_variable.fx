float4 k;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float3 r0;
	half3 r1;
	r0 = saturate(texcoord.xyz);
	r0 = half3(r0.xyz * k.xxx);
	r1 = r0.xyz * k.yyy + r0.xyz;
	o.xyz = r0.xyz * r1.xyz;
	o.w = 1;

	return o;
}
