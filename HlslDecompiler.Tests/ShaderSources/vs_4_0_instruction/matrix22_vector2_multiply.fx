float2x2 matrix_2x2;

struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float4 r0;
	r0 = position.yyxx * transpose(matrix_2x2)[1].xyxy;
	o.position = transpose(matrix_2x2)[0].xyxy * position.xxyy + r0;
	r0.xy = position.xy + position.xy;
	r0.yz = r0.yy * transpose(matrix_2x2)[1].xy;
	o.position1.zw = transpose(matrix_2x2)[0].xy * r0.xx + r0.yz;
	r0.xy = abs(position.xx) * transpose(matrix_2x2)[1].xy;
	o.position1.xy = transpose(matrix_2x2)[0].xy * abs(position.yy) + r0.xy;

	return o;
}
