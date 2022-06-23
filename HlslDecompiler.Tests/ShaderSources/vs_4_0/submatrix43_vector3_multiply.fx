float4x4 matrix_4x4;

float4 main(float4 position : POSITION) : POSITION
{
	return mul((float4x3)matrix_4x4, position.xyz);
}
