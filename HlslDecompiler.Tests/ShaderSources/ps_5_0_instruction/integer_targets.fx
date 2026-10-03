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

	o.sv_target.y = i.sv_primitiveid * 7 + 3;
	o.sv_target.z = i.sv_primitiveid & 255;
	o.sv_target.x = (float)i.sv_primitiveid;
	o.sv_target.w = 1;
	o.sv_target1.z = (float)(uint)i.sv_primitiveid;
	o.sv_target1.xy = i.sv_position.xy * float2(0.00100000005, 0.00100000005);
	o.sv_target1.w = 1;

	return o;
}
