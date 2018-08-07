float4x4 matrix_4x4;

struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
	float4 position2 : POSITION2;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = mul(matrix_4x4, position);
	o.position1 = mul(matrix_4x4, position.yxzw);
	o.position2 = mul(matrix_4x4, abs(position.yxzw));

	return o;
}
