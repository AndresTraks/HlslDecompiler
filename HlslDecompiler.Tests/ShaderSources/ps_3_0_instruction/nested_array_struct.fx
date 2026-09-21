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
	float4 o;

	float2 r0;
	r0.x = saturate(dot(g_Lighting.lights[1].direction.xyz, texcoord.xyz));
	r0.x = r0.x * g_Lighting.lights[1].intensity;
	r0.y = saturate(dot(g_Lighting.lights[0].direction.xyz, texcoord.xyz));
	r0.x = r0.y * g_Lighting.lights[0].intensity + r0.x;
	o.xyz = r0.xxx + g_Lighting.ambient.xyz;
	o.w = g_Lighting.ambient.w;

	return o;
}
