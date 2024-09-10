struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
};

PS_OUT main()
{
	PS_OUT o;

	o.color = float4(0.3, 0.7, 0.01, -50.01);
	o.color1 = float4(-1234567, 50.01, 5.01, 0);

	return o;
}
