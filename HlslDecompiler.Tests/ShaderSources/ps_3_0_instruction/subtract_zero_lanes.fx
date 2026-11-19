float bump;
float3 lightDir;
sampler2D s0;

float4 main(float2 texcoord : TEXCOORD) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	r0 = half4(tex2D(s0, texcoord.xy));
	r0.xyz = r0.xyz + float3(-0, -0, -1);
	r1.zw = float2(0, 1);
	r0.xyz = half3(bump.xxx * r0.xyz + r1.zzw);
	o.xyz = dot(r0.xyz, lightDir.xyz);
	o.w = 1;

	return o;
}
