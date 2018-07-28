float3x3 matrix_3x3;

float4 main(float4 position : POSITION) : POSITION
{
	return float4(mul(position.xyz, matrix_3x3), 4);
}
