struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	float4 r0;
	float4 r1;
	r0 = float4(exp2(floor(position.x)), position.x - floor(position.x), exp2(position.x), 1);
	r1 = log2(position.y);
	o.position = r0;
	o.color = r1;

	return o;
}
