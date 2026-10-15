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

	o.sv_target = asfloat(asint(i.texcoord) + i.u);
	o.sv_depth = i.texcoord.x > 0 ? asfloat((1056964608 & ~8388607) | (i.u & 8388607)) : i.texcoord.y;

	return o;
}
