struct PS_OUT
{
	float depth : DEPTH;
	float4 color : COLOR;
	float4 color1 : COLOR1;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.depth = saturate(texcoord.x);
	o.color = texcoord;
	o.color1 = texcoord.wzyx;

	return o;
}
