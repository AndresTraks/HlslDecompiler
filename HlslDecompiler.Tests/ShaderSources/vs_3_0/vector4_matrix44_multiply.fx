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

	o.position = mul(position, matrix_4x4);
	o.position1 = mul(position.yxzw, matrix_4x4);
	o.position2 = mul(abs(position.yxzw), matrix_4x4);
	o.position3 = mul(float4(5, 2, 3, 4) * position, matrix_4x4);
	o.position4 = mul(float4(position.xyz, 1), matrix_4x4);

	return o;
}
