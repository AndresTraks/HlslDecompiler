float4x4 matrix_4x4;

struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float4 position2 : POSITION2;
	float4 position3 : POSITION3;
	float4 position4 : POSITION4;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	r0 = transpose(matrix_4x4)[1] * position.y;
	r0 = transpose(matrix_4x4)[0] * position.x + r0;
	r0 = transpose(matrix_4x4)[2] * position.z + r0;
	o.position = transpose(matrix_4x4)[3] * position.w + r0;
	o.position4 = r0 + transpose(matrix_4x4)[3];
	r0 = transpose(matrix_4x4)[1] * position.x;
	r0 = transpose(matrix_4x4)[0] * position.y + r0;
	r0 = transpose(matrix_4x4)[2] * position.z + r0;
	o.position1 = transpose(matrix_4x4)[3] * position.w + r0;
	r0 = transpose(matrix_4x4)[1] * abs(position.x);
	r0 = transpose(matrix_4x4)[0] * abs(position.y) + r0;
	r0 = transpose(matrix_4x4)[2] * abs(position.z) + r0;
	o.position2 = transpose(matrix_4x4)[3] * abs(position.w) + r0;
	r0 = float4(5, 2, 3, 4) * position;
	r1 = r0.y * transpose(matrix_4x4)[1];
	r1 = transpose(matrix_4x4)[0] * r0.x + r1;
	r1 = transpose(matrix_4x4)[2] * r0.z + r1;
	o.position3 = transpose(matrix_4x4)[3] * r0.w + r1;

	return o;
}
