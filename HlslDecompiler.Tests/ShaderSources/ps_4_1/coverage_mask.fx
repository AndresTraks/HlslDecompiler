struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = texcoord;
	o.sv_coverage = texcoord.w > 0.5 ? 15 : 5;

	return o;
}
