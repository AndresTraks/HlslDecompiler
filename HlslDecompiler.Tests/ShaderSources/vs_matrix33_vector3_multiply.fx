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

	o.position = float4(mul(matrix_3x3, position.xyz), 1);
	o.position1 = mul(matrix_3x3, position.yxz);
	o.position2 = mul(matrix_3x3, abs(position.yxz));
	o.position3 = mul(matrix_3x3, float3(2 * position.x, position.y, 3 * position.z));

	return o;
}
