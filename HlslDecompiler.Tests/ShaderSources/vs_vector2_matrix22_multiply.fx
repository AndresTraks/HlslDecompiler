float2x2 matrix_2x2;

float4 main(float4 position : POSITION) : POSITION
{
	return float4(mul(position.yx, matrix_2x2), float2(1, 2));
}
