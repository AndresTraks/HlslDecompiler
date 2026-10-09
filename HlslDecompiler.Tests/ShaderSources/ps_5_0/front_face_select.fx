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

	o.sv_target = i.sv_isfrontface ? float4(i.texcoord, 0, 1) : float4(0, i.texcoord, 1);
	o.sv_depth = saturate(0.00999999978 * i.texcoord.x + i.sv_position.z);
	o.sv_coverage = i.texcoord.y > 0.5 ? 15 : 3;

	return o;
}
