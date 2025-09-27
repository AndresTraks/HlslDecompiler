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

	float4 r0;
	r0 = transpose(matrix_2x3)[1].xyxy * position.yyxx;
	o.position = transpose(matrix_2x3)[0].xyxy * position.xxyy + r0;
	r0.xy = transpose(matrix_2x3)[1].xy * abs(position.xx);
	o.position1 = transpose(matrix_2x3)[0].xy * abs(position.yy) + r0.xy;
	r0.xy = position.xy + position.xy;
	r0.yz = r0.yy * transpose(matrix_2x3)[1].xy;
	o.position2 = transpose(matrix_2x3)[0].xy * r0.xx + r0.yz;

	return o;
}
