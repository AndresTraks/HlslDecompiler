float4 eyePosition : register(c1);
float4 lightDirection;
sampler2D normalMap;
float4 waterColour : register(c2);

struct PS_IN
{
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = normalize(2 * tex2D(normalMap, i.texcoord).xyz - 1).xyz;
	float t1 = 2 * dot(-lightDirection.xyz, t0);
	float3 t2 = normalize(eyePosition.xyz - i.texcoord1).xyz;
	float t3 = 1 - saturate(dot(t0, t2));
	float t4 = t3 * t3;
	float t5 = t3 * t4 * t4;
	float t6 = pow(saturate(dot(t0 * -t1 - lightDirection.xyz, t2)), 32);
	return float4(t6 + waterColour.xyz + t5 * waterColour.w, 1);
}
