struct struct1
{
	float4x4 world;
	float4 tint;
};

int g_Index : register(c20);
struct1 g_Instances[4];

float4 main(float4 position : POSITION) : POSITION
{
	return float4(dot(position, transpose(g_Instances[g_Index].world)[0]), dot(position, transpose(g_Instances[g_Index].world)[1]), dot(position, transpose(g_Instances[g_Index].world)[2]), dot(position, transpose(g_Instances[g_Index].world)[3])) + g_Instances[g_Index].tint;
}
