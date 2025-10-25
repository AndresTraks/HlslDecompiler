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
	o.position.x = dot(position, transpose(matrix_4x4)[0]);
	o.position.y = dot(position, transpose(matrix_4x4)[1]);
	o.position.z = dot(position, transpose(matrix_4x4)[2]);
	o.position.w = dot(position, transpose(matrix_4x4)[3]);
	o.position1.x = dot(position.yxzw, transpose(matrix_4x4)[0]);
	o.position1.y = dot(position.yxzw, transpose(matrix_4x4)[1]);
	o.position1.z = dot(position.yxzw, transpose(matrix_4x4)[2]);
	o.position1.w = dot(position.yxzw, transpose(matrix_4x4)[3]);
	o.position2.x = dot(abs(position.yxzw), transpose(matrix_4x4)[0]);
	o.position2.y = dot(abs(position.yxzw), transpose(matrix_4x4)[1]);
	o.position2.z = dot(abs(position.yxzw), transpose(matrix_4x4)[2]);
	o.position2.w = dot(abs(position.yxzw), transpose(matrix_4x4)[3]);
	r0 = float4(5, 2, 3, 4) * position;
	o.position3.x = dot(r0, transpose(matrix_4x4)[0]);
	o.position3.y = dot(r0, transpose(matrix_4x4)[1]);
	o.position3.z = dot(r0, transpose(matrix_4x4)[2]);
	o.position3.w = dot(r0, transpose(matrix_4x4)[3]);
	r0 = position.xyzx * float4(1, 1, 1, 0) + float4(0, 0, 0, 1);
	o.position4.x = dot(r0, transpose(matrix_4x4)[0]);
	o.position4.y = dot(r0, transpose(matrix_4x4)[1]);
	o.position4.z = dot(r0, transpose(matrix_4x4)[2]);
	o.position4.w = dot(r0, transpose(matrix_4x4)[3]);

	return o;
}
