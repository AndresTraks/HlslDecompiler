struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
};

VS_OUT main(float4 position : POSITION)
{
	VS_OUT o;

	o.color = log2(position.y);
	o.position = float4(exp2(floor(position.x)), frac(position.x), exp2(position.x), 1);

	return o;
}
