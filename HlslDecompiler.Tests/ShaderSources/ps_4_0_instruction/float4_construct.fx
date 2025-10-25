struct PS_OUT
{
	float4 sv_target : SV_Target;
	float4 sv_target1 : SV_Target1;
	float4 sv_target2 : SV_Target2;
	float4 sv_target3 : SV_Target3;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = texcoord;
	o.sv_target1 = float4(texcoord.xyz, 0);
	o.sv_target2 = float4(texcoord.xy, 0, 1);
	o.sv_target3 = float4(texcoord.x, 0, 1, 2);

	return o;
}
