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
	float t0 = saturate(dot(normalize(i.texcoord1), -lightDirection));
	float t1 = pow(1 - saturate(dot(normalize(i.texcoord1), normalize(i.texcoord2))), rimPower);
	float2 t2 = i.texcoord * layerScale;
	float3 t3 = tex2D(splatMap, i.texcoord).xyz;
	return float4(lerp(fogColour.xyz, (tex2D(layer0, t2).xyz * t3.x + tex2D(layer1, t2).xyz * t3.y + tex2D(layer2, t2).xyz * t3.z) * t0 + t1, i.texcoord3), 1);
}
