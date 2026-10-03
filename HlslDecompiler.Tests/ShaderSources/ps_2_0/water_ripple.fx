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
	float3 t0 = normalize(eyePosition.xyz - i.texcoord1);
	float3 t1 = tex2D(normalMap, i.texcoord).xyz;
	float3 t2 = normalize(2 * t1 - 1);
	float t3 = 1 - saturate(dot(t2, t0));
	float t4 = t3 * t3;
	float t5 = t3 * t4 * t4;
	float t6 = pow(saturate(dot(reflect(-lightDirection.xyz, t2), t0)), 32);
	return float4(t6 + waterColour.xyz + t5 * waterColour.w, 1);
}
