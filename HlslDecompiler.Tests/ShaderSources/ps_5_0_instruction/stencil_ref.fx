struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_stencilref : SV_StencilRef;
};

PS_OUT main(noperspective float4 sv_position : SV_Position)
{
	PS_OUT o;

	o.sv_target = sv_position;
	o.sv_stencilref = (uint)sv_position.x;

	return o;
}
