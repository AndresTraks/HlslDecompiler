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
	float t6 = roughnessMap.Sample(linearSampler, i.texcoord1).x;
	float t7 = max(t6 * t6, 0.00200000009);
	float t8 = 0.5 * -t7 + 1;
	float t9 = 0.5 * t7;
	float3 t10 = normalize(i.normal);
	float t11 = saturate(dot(t10, t2));
	float t12 = t11 * t11 * (t7 * t7 - 1) + 1;
	float t13 = saturate(dot(t10, -lightDirection));
	float t14 = t7 * t7 / (3.14159274 * t12 * t12) / ((saturate(dot(t10, t0)) * t8 + t9) * (t13 * t8 + t9));
	float3 t15 = albedoMap.Sample(linearSampler, i.texcoord1).xyz;
	float3 t16 = metallic * (t15 - 0.0399999991) + 0.0399999991;
	float3 t17 = lerp(t16, 1, t5);
	float3 t18 = environment.SampleLevel(linearSampler, reflect(-t0, t10), 8 * t6).xyz;
	return float4(t13 * ((1 - metallic) * t15 * (1 - t17) + 0.25 * t14 * t17) * lightIntensity + t16 * t18, 1);
}
