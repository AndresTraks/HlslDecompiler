struct PS_IN
{
	noperspective float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	nointerpolation uint sv_isfrontface : SV_IsFrontFace;
};

struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
	float sv_depth : SV_Depth;
};

PS_OUT main(PS_IN i)
{
	PS_OUT o;

	int4 r0;
	r0.xy = asint(i.texcoord.xy);
	r0.zw = int2(0, 1065353216);
	o.sv_target = (i.sv_isfrontface != 0) ? asfloat(r0) : asfloat(r0.zxyw);
	o.sv_depth = saturate(i.texcoord.x * 0.00999999978 + i.sv_position.z);
	r0.x = (0.5 < i.texcoord.y) ? -1 : 0;
	o.sv_coverage = (r0.x != 0) ? 15 : 3;

	return o;
}
