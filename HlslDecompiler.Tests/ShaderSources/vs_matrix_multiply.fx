float2x2 matrix_2x2;

float4 main(float4 position : POSITION) : POSITION
{
	return float4(mul(matrix_2x2, position.wy), float2(0, 1));
}
