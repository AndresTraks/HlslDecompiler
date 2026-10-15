struct PS_IN
{
	float4 sv_position : SV_Position;
	float4 c : C;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
	uint sv_stencilref : SV_StencilRef;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	o.sv_target = i.c;
	o.sv_coverage = i.c.y > 0.5 ? 15 : asint(i.c.z);
	o.sv_stencilref = asint(i.c.x) & 255;

	return o;
}
