float3 lightDir;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	half4 r0;
	r0 = tex2D(s0, texcoord.xy);
	r0.xyz = r0.xyz * 2 + float3(-1, -1, -2);
	r0.xyz = r0.www * r0.xyz + float3(0, 0, 1);
	o.xyz = dot(r0.xyz, lightDir.xyz);
	o.w = half(1);

	return o;
}
