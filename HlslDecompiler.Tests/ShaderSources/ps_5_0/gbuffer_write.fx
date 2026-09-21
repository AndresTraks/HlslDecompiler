cbuffer Material : register(b0)
{
	float4 baseColor;
	float roughness;
	float metallic;
	uint mode;
	float alphaCutoff;
};

SamplerState anisoSampler;
Texture2D albedoMap;
Texture2D normalMap;
Texture2D maskMap;

struct PS_IN
{
	float3 normal : NORMAL;
	float4 tangent : TANGENT;
	float2 texcoord : TEXCOORD;
	float3 texcoord1 : TEXCOORD1;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float2 sv_target2 : SV_Target2;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	float3 t0 = albedoMap.Sample(anisoSampler, i.texcoord).xyz;
	float t1 = albedoMap.Sample(anisoSampler, i.texcoord).w;
	float3 t2 = t0 * baseColor.xyz;
	float4 t3 = maskMap.Sample(anisoSampler, i.texcoord);
	switch (mode) {
		case 0:
			clip(t1 * baseColor.w - alphaCutoff);
			break;
		case 1:
			t2 = t2 * t3.x;
			break;
		case 2:
			t2 = t3.w * (-t0 * baseColor.xyz + t3.xyz) + t2;
			break;
		default:
			break;
	}
	float2 t4 = 2 * normalMap.Sample(anisoSampler, i.texcoord).xy - 1;
	float t5 = sqrt(max(1 - dot(t4, t4), 0));
	float3 t6 = normalize(i.normal);
	float t7 = dot(i.tangent.xyz, t6);
	float3 t8 = -t6 * t7 + i.tangent.xyz;
	float3 t9 = normalize(t8);
	float3 t10 = t4.x * t9 + (cross(t6, t9)) * i.tangent.w * t4.y + t5 * t6;
	float3 t11 = normalize(t10);
	float t12 = 1 - saturate(dot(t11, normalize(i.texcoord1)));
	float t13 = t12 * t12;
	o.sv_target = float4(t2, t12 * t13 * t13);
	o.sv_target1 = float4(0.5 * t11 + 0.5, mode == 2 ? t3.w : 1);
	o.sv_target2 = t3.yz * float2(roughness, metallic);

	return o;
}
