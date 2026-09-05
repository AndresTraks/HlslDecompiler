float4 a;
float4 b;
float4 c;

struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
	float depth : DEPTH;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.color = a * texcoord;
	o.color1 = b + texcoord;
	o.depth = c.x;

	return o;
}
