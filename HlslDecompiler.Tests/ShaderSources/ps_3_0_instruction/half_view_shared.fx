float3 cameraPos;
float3 lightDir;
float gloss;
sampler2D albedo;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	float2 texcoord2 : TEXCOORD2;
};

half4 main(PS_IN i) : COLOR
{
	half4 o;

	float4 r0;
	half3 r1;
	float4 r2;
	r0.xyz = cameraPos.xyz + -i.texcoord.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r1 = r0.xyz * r0.www + lightDir.xyz;
	r0.xyz = half3(r0.www * r0.xyz);
	r0.x = half(saturate(dot(i.texcoord1.xyz, r0.xyz)));
	r0.x = half(-r0.x + 1);
	r2.xyz = half3(normalize(r1.xyz));
	r0.y = half(saturate(dot(i.texcoord1.xyz, r2.xyz)));
	r1.x = pow(r0.y, gloss.x);
	r2 = tex2D(albedo, i.texcoord2.xy);
	o = r2 * r0.x + r1.x;

	return o;
}
