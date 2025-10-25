struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
};

PS_OUT main()
{
	PS_OUT o;

	o.sv_target = float4(0.300000012, 0.699999988, 0.00999999978, -50.0099983);
	o.sv_target1 = float4(-1234567, 50.0099983, 5.01000023, 0);

	return o;
}
