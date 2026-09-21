struct struct1
{
	float3 direction;
	float intensity;
};

struct struct2
{
	struct1 lights[2];
	float4 ambient;
	float2 falloff[2];
};

cbuffer Params : register(b0)
{
	struct2 g_Lighting;
};

float4 main(float3 texcoord : TEXCOORD) : SV_Target
{
	return float4((g_Lighting.lights[0].direction * g_Lighting.lights[0].intensity + g_Lighting.lights[1].intensity * g_Lighting.lights[1].direction) * texcoord + g_Lighting.ambient.xyz, g_Lighting.falloff[0].x + g_Lighting.falloff[1].y);
}
