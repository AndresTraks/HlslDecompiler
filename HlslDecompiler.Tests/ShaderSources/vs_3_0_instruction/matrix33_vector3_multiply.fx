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

	float4 r0;
	float3 r1;
	r0.xyz = transpose(matrix_3x3)[1].xyz * position.yyy;
	r1.xyz = transpose(matrix_3x3)[0].xyz * position.xxx + r0.xyz;
	o.position.xyz = transpose(matrix_3x3)[2].xyz * position.zzz + r1.xyz;
	r1.xyz = transpose(matrix_3x3)[1].xyz * position.xxx;
	r1.xyz = transpose(matrix_3x3)[0].xyz * position.yyy + r1.xyz;
	o.position1 = transpose(matrix_3x3)[2].xyz * position.zzz + r1.xyz;
	r1.xyz = transpose(matrix_3x3)[1].xyz * abs(position.xxx);
	r1.xyz = transpose(matrix_3x3)[0].xyz * abs(position.yyy) + r1.xyz;
	o.position2 = transpose(matrix_3x3)[2].xyz * abs(position.zzz) + r1.xyz;
	r0.w = position.x + position.x;
	r0.xyz = transpose(matrix_3x3)[0].xyz * r0.www + r0.xyz;
	r0.w = 3 * position.z;
	o.position3 = transpose(matrix_3x3)[2].xyz * r0.www + r0.xyz;
	o.position.w = 1;

	return o;
}
