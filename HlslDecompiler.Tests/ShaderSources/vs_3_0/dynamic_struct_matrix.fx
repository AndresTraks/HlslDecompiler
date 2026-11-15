struct struct1
{
	float4x4 world;
	float4 tint;
};

struct1 g_Instances[4];
int g_Index;

float4 main(float4 position : POSITION) : POSITION
{
	return mul(position, g_Instances[g_Index].world) + g_Instances[g_Index].tint;
}
