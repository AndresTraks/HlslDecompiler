struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
};

PS_OUT main()
{
	PS_OUT o;

	o.color = 0;
	o.color1 = 0;

	return o;
}
