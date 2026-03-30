float2x2 matrix_2x2;

struct VS_OUT
{
	float4 position : POSITION;
	float4 position1 : POSITION1;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float2 r0;
	o.position.x = dot(position.xy, transpose(matrix_2x2)[0].xy);
	o.position.y = dot(position.xy, transpose(matrix_2x2)[1].xy);
	o.position.z = dot(position.yx, transpose(matrix_2x2)[0].xy);
	o.position.w = dot(position.yx, transpose(matrix_2x2)[1].xy);
	o.position1.x = dot(abs(position.yx), transpose(matrix_2x2)[0].xy);
	o.position1.y = dot(abs(position.yx), transpose(matrix_2x2)[1].xy);
	r0 = position.xy + position.xy;
	o.position1.z = dot(r0.xy, transpose(matrix_2x2)[0].xy);
	o.position1.w = dot(r0.xy, transpose(matrix_2x2)[1].xy);

	return o;
}
