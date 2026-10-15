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
	o.sv_target = asfloat(i.texcoord + i.u);
	r0.x = (0 < i.texcoord.x) ? -1 : 0;
	r0.y = (1056964608 & ~(((1 << 23) - 1) << 0)) | ((i.u << 0) & (((1 << 23) - 1) << 0));
	o.sv_depth = (r0.x != 0) ? (float)r0.y : (float)i.texcoord.y;

	return o;
}
