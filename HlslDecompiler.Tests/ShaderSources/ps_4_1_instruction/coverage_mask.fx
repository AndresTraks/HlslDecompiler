struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	int r0;
	o.sv_target = texcoord;
	r0 = (0.5 < texcoord.w) ? -1 : 0;
	o.sv_coverage = (r0.x != 0) ? 15 : 5;

	return o;
}
