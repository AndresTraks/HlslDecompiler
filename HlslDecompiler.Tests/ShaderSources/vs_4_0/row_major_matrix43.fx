row_major float4x3 g_World;
row_major float4x3 g_Bones[3];

float4 main(float4 position : POSITION) : POSITION
{
	return float4(mul(position, g_World) + mul(position, g_Bones[2]), 1);
}
