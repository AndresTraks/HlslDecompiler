struct struct1
{
	float3 position;
	float range;
	float4 color;
};

struct1 g_PointLight[2];

float4 main(float3 texcoord : TEXCOORD) : COLOR
{
	float3 t0 = g_PointLight[1].position - texcoord;
	float t1 = rcp(dot(t0, t0));
	return float4(t1 * g_PointLight[1].color.xyz * g_PointLight[1].range, 1);
}
