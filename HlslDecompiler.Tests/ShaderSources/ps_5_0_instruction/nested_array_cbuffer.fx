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
	float4 o;

	float3 r0;
	r0 = g_Lighting.lights[1].intensity * g_Lighting.lights[1].direction.xyz;
	r0 = g_Lighting.lights[0].direction.xyz * g_Lighting.lights[0].intensity + r0.xyz;
	o.xyz = r0.xyz * texcoord.xyz + g_Lighting.ambient.xyz;
	o.w = g_Lighting.falloff[0].x + g_Lighting.falloff[1].y;

	return o;
}
