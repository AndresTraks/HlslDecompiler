float scale;

struct VS_IN
{
	float4 position : POSITION;
	float4 color : COLOR;
};

struct VS_OUT
{
	float4 color : COLOR;
	float4 position : POSITION;
	float psize : PSIZE;
};

VS_OUT main(VS_IN i)
{
	VS_OUT o;

	o.color = i.color;
	o.position = i.position;
	o.psize = scale / i.position.w;

	return o;
}
