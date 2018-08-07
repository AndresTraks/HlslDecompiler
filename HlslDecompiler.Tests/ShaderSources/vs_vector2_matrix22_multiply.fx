float2x2 matrix_2x2;

struct VS_OUT
{
	float4 position : POSITION;
	float2 position1 : POSITION1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(position.xy, matrix_2x2), mul(position.yx, matrix_2x2));
	o.position1 = mul(abs(position.yx), matrix_2x2);

	return o;
}
