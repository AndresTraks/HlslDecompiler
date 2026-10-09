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
	float4 sv_position : SV_Position;
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
			t2 = t2 + t3.w * (-t0 * baseColor.xyz + t3.xyz);
			break;
		default:
			break;
	}
	float3 t4 = normalize(i.normal);
	float t5 = dot(i.tangent.xyz, t4);
	float3 t6 = -t4 * t5 + i.tangent.xyz;
	float3 t7 = normalize(t6);
	float2 t8 = normalMap.Sample(anisoSampler, i.texcoord).xy;
	float2 t9 = 2 * t8 - 1;
	float t10 = sqrt(max(1 - dot(t9, t9), 0));
	float3 t11 = t9.x * t7 + cross(t4, t7) * i.tangent.w * t9.y + t10 * t4;
	float3 t12 = normalize(t11);
	float t13 = 1 - saturate(dot(t12, normalize(i.texcoord1)));
	float t14 = t13 * t13;
	o.sv_target = float4(t2, t13 * t14 * t14);
	o.sv_target1 = float4(0.5 * t12 + 0.5, mode == 2 ? t3.w : 1);
	o.sv_target2 = t3.yz * float2(roughness, metallic);

	return o;
}
