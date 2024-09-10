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
	o.color1 = float4(1, 1, 1, 0) * texcoord.xyzx;
	o.color2 = texcoord.xyxx * float4(1, 1, 0, 0) + float4(0, 0, 0, 1);
	o.color3 = texcoord.x * float4(1, 0, 0, 0) + float4(0, 0, 1, 2);

	return o;
}
