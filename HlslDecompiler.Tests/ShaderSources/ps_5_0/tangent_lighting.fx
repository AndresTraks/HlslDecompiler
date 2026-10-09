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
	float4 sv_position : SV_Position;
	float3 position : POSITION;
	float3 normal : NORMAL;
	float3 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(i.normal);
	float t1 = dot(i.tangent, t0);
	float3 t2 = -t0 * t1 + i.tangent;
	float3 t3 = normalize(t2);
	float3 t4 = cameraPosition - i.position;
	float t5 = saturate((length(t4) - fogRange.x) / (fogRange.y - fogRange.x));
	float3 t6 = normalize(t4) - lightDirection;
	float3 t7 = albedoMap.Sample(linearSampler, i.texcoord).xyz;
	float3 t8 = normalMap.Sample(linearSampler, i.texcoord).xyz;
	float3 t9 = 2 * t8 - 1;
	float3 t10 = t9.x * t3 + cross(t0, t3) * t9.y + t9.z * t0;
	float3 t11 = normalize(t10);
	float t12 = saturate(dot(t11, -lightDirection)) * lightIntensity;
	float t13 = (flags & 1 ? 1.0 : 0.0) * pow(saturate(dot(t11, normalize(t6))), 32);
	float3 t14 = t7 * t12 + t13;
	return float4(flags & 2 ? lerp(t14, fogColor.xyz, t5) : t14, albedoMap.Sample(linearSampler, i.texcoord).w);
}
