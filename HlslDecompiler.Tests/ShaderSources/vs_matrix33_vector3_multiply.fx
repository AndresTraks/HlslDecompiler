float3x3 matrix_3x3;

float4 main(float4 position : POSITION) : POSITION
{
	return float4(mul(matrix_3x3, position.xyz), 4);
}
