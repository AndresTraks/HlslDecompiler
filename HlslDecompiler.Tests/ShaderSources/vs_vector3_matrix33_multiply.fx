float3x3 matrix_3x3;

struct VS_OUT
{
	float4 position : POSITION;
	float3 position1 : POSITION1;
	float3 position2 : POSITION2;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(position.xyz, matrix_3x3), 1);
	o.position1 = mul(position.yxz, matrix_3x3);
	o.position2 = mul(abs(position.yxz), matrix_3x3);

	return o;
}
