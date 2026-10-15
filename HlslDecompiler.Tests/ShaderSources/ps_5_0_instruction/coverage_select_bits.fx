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

	int r0;
	o.sv_target = i.c;
	o.sv_stencilref = asfloat(asint(i.c.x) & 255);
	r0 = (0.5 < i.c.y) ? -1 : 0;
	o.sv_coverage = (r0.x != 0) ? 15 : (int)i.c.z;

	return o;
}
