float2x3 matrix_2x3;

struct VS_OUT
{
	float4 position : POSITION;
	float2 position1 : POSITION1;
	float2 position2 : POSITION2;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(matrix_2x3, position.xy), mul(matrix_2x3, position.yx));
	o.position1 = mul(matrix_2x3, abs(position.yx));
	o.position2 = mul(matrix_2x3, 2 * position.xy);

	return o;
}
