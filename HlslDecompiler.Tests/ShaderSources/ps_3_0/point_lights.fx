float3 cameraPosition : register(c8);
sampler2D diffuseMap;
samplerCUBE envMap : register(s2);
float3 lightColors[4] : register(c4);
int lightCount : register(i0);
float3 lightPositions[4];
sampler2D normalMap;
float specularPower : register(c9);
float2 uvScale : register(c10);

struct PS_IN
{
	float3 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
	float3 texcoord2 : TEXCOORD2;
	float2 texcoord3 : TEXCOORD3;
	float4 color : COLOR;
};

float4 main(PS_IN i) : COLOR
{
	float3 t0 = normalize(i.texcoord1);
	float t1 = dot(i.texcoord2, t0);
	float3 t2 = t0 * -t1 + i.texcoord2;
	float3 t3 = normalize(t2);
	float4 t4 = tex2D(diffuseMap, uvScale * i.texcoord3);
	float3 t5 = tex2D(normalMap, uvScale * i.texcoord3).xyz;
	float3 t6 = 2 * t5 - 1;
	float4 t7 = float4(0.100000001, 0.100000001, 0.100000001, 0);
	float4 t8 = t4 * i.color;
	float3 t9 = normalize(t6.x * t3 + cross(t0, t3) * t6.y + t6.z * t0);
	float3 t10 = normalize(cameraPosition - i.texcoord);
	for (int i_ = 0; i_ < lightCount; i_++) {
		float3 t11 = lightPositions[t7.w] - i.texcoord;
		float3 t12 = normalize(t11);
		float t13 = pow(saturate(dot(t9, normalize(t12 + t10))), specularPower) + saturate(dot(t9, t12));
		float t14 = dot(t11, t11) + 1;
		t7.xyz = t7.xyz + t13 * lightColors[t7.w] / t14;
		t7.w = t7.w + 1;
	}
	float t15 = 1 - saturate(dot(t9, t10));
	float t16 = t15 * t15;
	float t17 = t16 * t16;
	float3 t18 = texCUBE(envMap, reflect(-t10, t9)).xyz;
	return float4(t8.xyz * t7.xyz + t17 * t18, t8.w);
}
