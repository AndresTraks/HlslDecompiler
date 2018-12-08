struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
	float4 color2 : COLOR2;
	float4 color3 : COLOR3;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.color = texcoord;
	o.color1 = float4(texcoord.xyz, 0);
	o.color2 = float4(texcoord.xy, 0, 1);
	o.color3 = float4(texcoord.x, 0, 1, 2);

	return o;
}
