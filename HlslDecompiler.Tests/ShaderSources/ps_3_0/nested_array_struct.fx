struct struct1
{
	float3 direction;
	float intensity;
};

struct struct2
{
	struct1 lights[2];
	float4 ambient;
};

struct2 g_Lighting;

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float t0 = saturate(dot(g_Lighting.lights[0].direction, texcoord)) * g_Lighting.lights[0].intensity + saturate(dot(g_Lighting.lights[1].direction, texcoord)) * g_Lighting.lights[1].intensity;
	return float4(t0 + g_Lighting.ambient.xyz, g_Lighting.ambient.w);
}
