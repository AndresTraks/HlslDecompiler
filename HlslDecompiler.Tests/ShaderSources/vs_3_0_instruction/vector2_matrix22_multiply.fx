float2x2 matrix_2x2;

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
	r0 = transpose(matrix_2x2)[0].xyxy * position.xyyx;
	o.position.xz = r0.yw + r0.xz;
	r0 = transpose(matrix_2x2)[1].xyxy * position.xyyx;
	o.position.yw = r0.yw + r0.xz;
	r0.xy = transpose(matrix_2x2)[0].xy * abs(position.yx);
	o.position1.x = r0.y + r0.x;
	r0.xy = transpose(matrix_2x2)[1].xy * abs(position.yx);
	o.position1.y = r0.y + r0.x;
	r0.xy = position.xy + position.xy;
	r0.zw = r0.xy * transpose(matrix_2x2)[0].xy;
	r0.xy = r0.xy * transpose(matrix_2x2)[1].xy;
	o.position2.y = r0.y + r0.x;
	o.position2.x = r0.w + r0.z;

	return o;
}
