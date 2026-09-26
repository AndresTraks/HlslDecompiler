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
	float3 t0 = normalize(eye - i.texcoord) - lightDirection;
	float3 t1 = normalize(t0);
	float3 t2 = normalize(eye - i.texcoord);
	float t3 = 1 - saturate(dot(t2, t1));
	float t4 = t3 * t3;
	float t5 = t4 * t4 * t3;
	float t6 = roughnessMap.Sample(linearSampler, i.texcoord1).x;
	float t7 = max(t6 * t6, 0.00200000009);
	float t8 = 0.5 * -t7 + 1;
	float t9 = 0.5 * t7;
	float3 t10 = albedoMap.Sample(linearSampler, i.texcoord1).xyz;
	float3 t11 = metallic * (t10 - 0.0399999991) + 0.0399999991;
	float3 t12 = lerp(t11, 1, t5);
	float3 t13 = normalize(i.normal);
	float t14 = saturate(dot(t13, t1));
	float t15 = t14 * t14 * (t7 * t7 - 1) + 1;
	float t16 = saturate(dot(t13, -lightDirection));
	float t17 = t7 * t7 / (3.14159274 * t15 * t15) / ((saturate(dot(t13, t2)) * t8 + t9) * (t16 * t8 + t9));
	float3 t18 = environment.SampleLevel(linearSampler, reflect(-t2, t13), 8 * t6).xyz;
	return float4(t16 * ((1 - metallic) * t10 * (1 - t12) + 0.25 * t17 * t12) * lightIntensity + t11 * t18, 1);
}
