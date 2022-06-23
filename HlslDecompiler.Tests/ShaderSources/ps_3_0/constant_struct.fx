struct PS_OUT
{
	float4 color : COLOR;
	float4 color1 : COLOR1;
};

PS_OUT main()
{
	PS_OUT o;

	o.color = float4(0.300000012, 0.699999988, 0.00999999978, -50.0099983);
	o.color1 = float4(-1234567, 50.0099983, 5.01000023, 0);

	return o;
}
