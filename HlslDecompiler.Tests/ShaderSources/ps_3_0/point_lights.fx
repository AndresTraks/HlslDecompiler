float3 cameraPosition : register(c8);
sampler2D diffuseMap;
samplerCUBE envMap : register(s2);
float3 lightColors[4] : register(c4);
int lightCount;
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
	float3 t4 = tex2D(normalMap, uvScale * i.texcoord3).xyz;
	float3 t5 = 2 * t4 - 1;
	float4 t6 = tex2D(diffuseMap, uvScale * i.texcoord3);
	float4 t7 = float4(0.100000001, 0.100000001, 0.100000001, 0);
	float4 t8 = t6 * i.color;
	float3 t9 = normalize(t5.x * t3 + cross(t0, t3) * t5.y + t5.z * t0);
	float3 t10 = normalize(cameraPosition - i.texcoord);
	for (int i_ = 0; i_ < lightCount; i_++) {
		float3 t11 = t7.w - float3(1, 2, 3);
		float3 t12 = (t11.z == 0 ? lightPositions[3] : t11.y == 0 ? lightPositions[2] : t11.x == 0 ? lightPositions[1] : t7.w == 0 ? lightPositions[0] : 0) - i.texcoord;
		float3 t13 = normalize(t12);
		float t14 = pow(saturate(dot(t9, normalize(t13 + t10))), specularPower) + saturate(dot(t9, t13));
		float t15 = dot(t12, t12) + 1;
		t7.xyz = t7.xyz + t14 * (t11.z == 0 ? lightColors[3] : t11.y == 0 ? lightColors[2] : t11.x == 0 ? lightColors[1] : t7.w == 0 ? lightColors[0] : 0) / t15;
		t7.w = t7.w + 1;
	}
	float t16 = 1 - saturate(dot(t9, t10));
	float t17 = t16 * t16;
	float t18 = t17 * t17;
	float3 t19 = texCUBE(envMap, reflect(-t10, t9)).xyz;
	return float4(t8.xyz * t7.xyz + t18 * t19, t8.w);
}
