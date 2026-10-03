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
	float2 t0 = i.texcoord * layerScale;
	float3 t1 = tex2D(splatMap, i.texcoord).xyz;
	float3 t2 = normalize(i.texcoord1);
	float t3 = saturate(dot(t2, -lightDirection));
	float t4 = pow(1 - saturate(dot(t2, normalize(i.texcoord2))), rimPower);
	return float4(lerp(fogColour.xyz, (tex2D(layer0, t0).xyz * t1.x + tex2D(layer1, t0).xyz * t1.y + tex2D(layer2, t0).xyz * t1.z) * t3 + t4, i.texcoord3), 1);
}
