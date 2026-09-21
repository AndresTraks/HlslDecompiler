cbuffer Frame : register(b0)
{
	float3 lightDirection;
	float lightIntensity;
	float3 cameraPosition;
	uint flags;
	float4 fogColor;
	float2 fogRange;
};

SamplerState linearSampler;
Texture2D albedoMap;
Texture2D normalMap;

struct PS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = cameraPosition - i.position;
	float t1 = saturate((length(t0) - fogRange.x) / (fogRange.y - fogRange.x));
	float3 t2 = normalize(t0) - lightDirection;
	float3 t3 = normalMap.Sample(linearSampler, i.texcoord).xyz;
	float3 t4 = 2 * t3 - 1;
	float3 t5 = albedoMap.Sample(linearSampler, i.texcoord).xyz;
	float3 t6 = normalize(i.normal);
	float t7 = dot(i.tangent, t6);
	float3 t8 = -t6 * t7 + i.tangent;
	float3 t9 = normalize(t8);
	float3 t10 = t4.x * t9 + (cross(t6, t9)) * t4.y + t4.z * t6;
	float3 t11 = normalize(t10);
	float t12 = saturate(dot(t11, -lightDirection)) * lightIntensity;
	float t13 = (flags & 1 ? 1.0 : 0.0) * pow(saturate(dot(t11, normalize(t2))), 32);
	float3 t14 = t5 * t12 + t13;
	return float4(flags & 2 ? lerp(t14, fogColor.xyz, t1) : t14, albedoMap.Sample(linearSampler, i.texcoord).w);
}
