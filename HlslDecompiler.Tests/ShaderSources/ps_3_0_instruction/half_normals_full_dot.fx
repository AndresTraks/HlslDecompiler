float3 cameraPos;
sampler2D albedo;
sampler2D bumps;

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	half3 texcoord1 : TEXCOORD1;
	half3 texcoord2 : TEXCOORD2;
	half3 texcoord3 : TEXCOORD3;
	float2 texcoord4 : TEXCOORD4;
};

half4 main(PS_IN i) : COLOR
{
	half4 o;

	float4 r0;
	float4 r1;
	half3 r2;
	r0 = tex2D(bumps, i.texcoord4.xy);
	r0.xyz = half3(r0.xyz * 2 + -1);
	r1.xyz = half3(r0.yyy * i.texcoord2.xyz);
	r0.xyw = half3(r0.xxx * i.texcoord1.xyz + r1.xyz);
	r0.xyz = half3(r0.zzz * i.texcoord3.xyz + r0.xyw);
	r1.xyz = half3(normalize(r0.xyz));
	r0.xyz = cameraPos.xyz + -i.texcoord.xyz;
	r2 = normalize(r0.xyz);
	r0.x = saturate(dot(r1.xyz, r2.xyz));
	r1 = tex2D(albedo, i.texcoord4.xy);
	o = r0.x * r1;

	return o;
}
