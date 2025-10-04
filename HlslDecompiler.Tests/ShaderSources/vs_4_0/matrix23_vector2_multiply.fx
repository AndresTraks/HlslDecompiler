float2x3 matrix_2x3;

struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(matrix_2x3, position.xy), mul(matrix_2x3, position.yx));
	o.position1 = float4(mul(matrix_2x3, abs(position.yx)), mul(matrix_2x3, 2 * position.xy));

	return o;
}
