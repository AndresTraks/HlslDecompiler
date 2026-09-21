struct struct1
{
	float3 position;
	float range;
	float4 color;
};

int g_Index : register(c24);
struct1 g_Lights[8];

float4 main(float4 position : POSITION) : POSITION
{
	float t0 = 3 * g_Index;
	return float4(g_Lights[t0 / 3].color.xyz * g_Lights[t0 / 3].range + g_Lights[t0 / 3].position + position.xyz, g_Lights[t0 / 3].color.w * g_Lights[t0 / 3].range + position.w + 1);
}
