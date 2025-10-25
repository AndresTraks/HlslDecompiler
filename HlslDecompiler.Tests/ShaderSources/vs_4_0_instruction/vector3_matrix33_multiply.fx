float3x3 matrix_3x3;

struct VS_OUT
{
	float4 position : POSITION;
	float3 position1 : POSITION1;
	float3 position2 : POSITION2;
	float3 position3 : POSITION3;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float3 r0;
	o.position.x = dot(position.xyz, transpose(matrix_3x3)[0].xyz);
	o.position.y = dot(position.xyz, transpose(matrix_3x3)[1].xyz);
	o.position.z = dot(position.xyz, transpose(matrix_3x3)[2].xyz);
	o.position1.x = dot(position.yxz, transpose(matrix_3x3)[0].xyz);
	o.position1.y = dot(position.yxz, transpose(matrix_3x3)[1].xyz);
	o.position1.z = dot(position.yxz, transpose(matrix_3x3)[2].xyz);
	o.position2.x = dot(abs(position.yxz), transpose(matrix_3x3)[0].xyz);
	o.position2.y = dot(abs(position.yxz), transpose(matrix_3x3)[1].xyz);
	o.position2.z = dot(abs(position.yxz), transpose(matrix_3x3)[2].xyz);
	r0 = float3(1, 2, 3) * position.yxz;
	o.position3.x = dot(r0.xyz, transpose(matrix_3x3)[0].xyz);
	o.position3.y = dot(r0.xyz, transpose(matrix_3x3)[1].xyz);
	o.position3.z = dot(r0.xyz, transpose(matrix_3x3)[2].xyz);
	o.position.w = 1;

	return o;
}
