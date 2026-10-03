struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	nointerpolation uint sv_primitiveid : SV_PrimitiveID;
};

struct PS_OUT
{
	uint4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.sv_target = int4(i.sv_primitiveid.xx * int2(1, 7) + int2(0, 3), i.sv_primitiveid & 255, 1);
	o.sv_target1 = float4(0.00100000005 * i.sv_position.xy, (float)i.sv_primitiveid, 1);

	return o;
}
