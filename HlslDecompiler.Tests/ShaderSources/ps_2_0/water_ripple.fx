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
	float3 t1 = normalize(eyePosition.xyz - i.texcoord1).xyz;
	float t2 = 1 - saturate(dot(t0, t1));
	float t3 = t2 * t2;
	float t4 = t2 * t3 * t3;
	float t5 = pow(saturate(dot(reflect(-lightDirection.xyz, t0), t1)), 32);
	return float4(t5 + waterColour.xyz + t4 * waterColour.w, 1);
}
