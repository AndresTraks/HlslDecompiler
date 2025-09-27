float2x2 matrix_2x2;

struct VS_OUT
{
	float4 position : POSITION;
	float2 texcoord : TEXCOORD;
	float2 texcoord1 : TEXCOORD1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.position = float4(mul(matrix_2x2, position.xy), mul(matrix_2x2, position.yx));
	o.texcoord = mul(matrix_2x2, abs(position.yx));
	o.texcoord1 = mul(matrix_2x2, 2 * position.xy);

	return o;
}
