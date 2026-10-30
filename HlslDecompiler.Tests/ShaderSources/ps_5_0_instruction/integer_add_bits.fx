struct PS_IN
{
	float4 texcoord : TEXCOORD;
	nointerpolation uint u : U;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	float sv_depth : SV_Depth;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	int2 r0;
	o.sv_target = asfloat(asint(i.texcoord) + i.u);
	r0.x = (0 < i.texcoord.x) ? -1 : 0;
	r0.y = (1056964608 & ~8388607) | (i.u & 8388607);
	o.sv_depth = (r0.x != 0) ? asfloat(r0.y) : i.texcoord.y;

	return o;
}
