float4x4 matrix_4x4;

float4 main(float4 position : POSITION) : POSITION
{
	float4 o;

	float4 r0;
	r0 = transpose(matrix_4x4)[1] * position.y;
	r0 = transpose(matrix_4x4)[0] * position.x + r0;
	o = transpose(matrix_4x4)[2] * position.z + r0;

	return o;
}
