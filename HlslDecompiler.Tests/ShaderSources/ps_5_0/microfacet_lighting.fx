cbuffer Params : register(b0)
{
	float3 lightDirection;
	float lightIntensity;
	float3 eye;
	float metallic;
};

SamplerState linearSampler;
Texture2D albedoMap;
Texture2D roughnessMap;
TextureCube environment;

struct PS_IN
{
	float4 sv_position : SV_Position;
	float3 texcoord : TEXCOORD;
	float3 normal : NORMAL;
	float2 texcoord1 : TEXCOORD1;
};

float4 main(PS_IN i) : SV_Target
{
	float3 t0 = normalize(eye - i.texcoord);
	float3 t1 = t0 - lightDirection;
	float3 t2 = normalize(t1);
	float t3 = 1 - saturate(dot(t0, t2));
	float t4 = t3 * t3;
	float t5 = t4 * t4 * t3;
	float3 t6 = albedoMap.Sample(linearSampler, i.texcoord1).xyz;
	float3 t7 = metallic * (t6 - 0.0399999991) + 0.0399999991;
	float3 t8 = lerp(t7, 1, t5);
	float3 t9 = normalize(i.normal);
	float t10 = saturate(dot(t9, t2));
	float t11 = saturate(dot(t9, -lightDirection));
	float t12 = roughnessMap.Sample(linearSampler, i.texcoord1).x;
	float t13 = max(t12 * t12, 0.00200000009);
	float t14 = t10 * t10 * (t13 * t13 - 1) + 1;
	float t15 = -0.5 * t13 + 1;
	float t16 = 0.5 * t13;
	float t17 = t13 * t13 / (3.14159274 * t14 * t14) / ((saturate(dot(t9, t0)) * t15 + t16) * (t11 * t15 + t16));
	float3 t18 = environment.SampleLevel(linearSampler, reflect(-t0, t9), 8 * t12).xyz;
	return float4(t11 * ((1 - metallic) * t6 * (1 - t8) + 0.25 * t17 * t8) * lightIntensity + t7 * t18, 1);
}
