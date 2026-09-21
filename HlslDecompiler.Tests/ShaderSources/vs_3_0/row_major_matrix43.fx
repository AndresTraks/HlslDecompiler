row_major float4x3 g_Bones[3];
row_major float4x3 g_World;

float4 main(float4 position : POSITION) : POSITION
{
	float3 t0 = float3(dot(float4(g_Bones[2][0].x, g_Bones[2][1].x, g_Bones[2][2].x, g_Bones[2][3].x), position), dot(float4(g_Bones[2][0].y, g_Bones[2][1].y, g_Bones[2][2].y, g_Bones[2][3].y), position), dot(float4(g_Bones[2][0].z, g_Bones[2][1].z, g_Bones[2][2].z, g_Bones[2][3].z), position));
	float3 t1 = float3(dot(float4(g_World[0].x, g_World[1].x, g_World[2].x, g_World[3].x), position), dot(float4(g_World[0].y, g_World[1].y, g_World[2].y, g_World[3].y), position), dot(float4(g_World[0].z, g_World[1].z, g_World[2].z, g_World[3].z), position));
	return float4(t1 + t0, 1);
}
