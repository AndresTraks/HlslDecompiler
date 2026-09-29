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
	float3 t0 = 2 * tex2D(normalMap, uvScale * i.texcoord3).xyz - 1;
	float3 t1 = normalize(i.texcoord1);
	float t2 = dot(i.texcoord2, t1);
	float3 t3 = t1 * -t2 + i.texcoord2;
	float3 t4 = normalize(t3);
	float4 t5 = float4(0.100000001, 0.100000001, 0.100000001, 0);
	float4 t6 = tex2D(diffuseMap, uvScale * i.texcoord3) * i.color;
	float3 t7 = normalize(t0.x * t4 + (cross(t1, t4)) * t0.y + t0.z * t1);
	float3 t8 = normalize(cameraPosition - i.texcoord);
	[loop]
	for (int i_ = 0; i_ < lightCount; i_++) {
		float3 t9 = t5.w - float3(1, 2, 3);
		float3 t10 = (t9.z == 0 ? lightPositions[3] : t9.y == 0 ? lightPositions[2] : t9.x == 0 ? lightPositions[1] : t5.w == 0 ? lightPositions[0] : 0) - i.texcoord;
		float t11 = pow(saturate(dot(t7, normalize(normalize(t10) + t8))), specularPower) + saturate(dot(t7, normalize(t10)));
		float t12 = rcp(dot(t10, t10) + 1);
		t5.xyz = t11 * (t9.z == 0 ? lightColors[3] : t9.y == 0 ? lightColors[2] : t9.x == 0 ? lightColors[1] : t5.w == 0 ? lightColors[0] : 0) * t12 + t5.xyz;
		t5.w = t5.w + 1;
	}
	float t13 = 1 - saturate(dot(t7, t8));
	float t14 = t13 * t13;
	float t15 = t14 * t14;
	return float4(t6.xyz * t5.xyz + t15 * texCUBE(envMap, reflect(-t8, t7)).xyz, t6.w);
}
