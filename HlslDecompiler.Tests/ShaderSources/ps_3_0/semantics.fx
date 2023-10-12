struct PS_OUT
{
	float4 color : COLOR;
	float depth : DEPTH;
};

PS_OUT main()
{
	PS_OUT o;

	o.color = float4(0.300000012, 0.699999988, 0.00999999978, -50.0099983);
	o.depth = -123456;

	return o;
}
