float4 fogColour;
sampler2D layer0 : register(s1);
sampler2D layer1 : register(s2);
sampler2D layer2 : register(s3);
float2 layerScale : register(c2);
float3 lightDirection;
float rimPower : register(c3);
sampler2D splatMap;

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float texcoord3 : TEXCOORD3;
};

float4 main(PS_IN i) : COLOR
{
	float4 o;

	float4 r0;
	float4 r1;
	float4 r2;
	float4 r3;
	r0.xy = i.texcoord.xy * layerScale.xy;
	r1 = tex2D(layer0, r0.xy);
	r2 = tex2D(layer1, r0.xy);
	r0 = tex2D(layer2, r0.xy);
	r3 = tex2D(splatMap, i.texcoord.xy);
	r2.xyz = r2.xyz * r3.yyy;
	r1.xyz = r1.xyz * r3.xxx + r2.xyz;
	r0.xyz = r0.xyz * r3.zzz + r1.xyz;
	r1.xyz = normalize(i.texcoord2.xyz);
	r2.xyz = normalize(i.texcoord1.xyz);
	r0.w = saturate(dot(r2.xyz, r1.xyz));
	r1.x = saturate(dot(r2.xyz, -lightDirection.xyz));
	r0.w = -r0.w + 1;
	r1.y = pow(r0.w, rimPower.x);
	r0.xyz = r0.xyz * r1.xxx + r1.yyy;
	r1.xyz = lerp(fogColour.xyz, r0.xyz, i.texcoord3.xxx);
	r1.w = 1;
	o = r1;

	return o;
}
