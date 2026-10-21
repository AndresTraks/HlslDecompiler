struct PS_OUT
{
	float4 sv_target : SV_Target;
	float2 sv_target1 : SV_Target1;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	o.sv_target = float4(texcoord.x > 0.5, texcoord.w, texcoord.z >= texcoord.y, 1);
	o.sv_target1 = float2(texcoord.z == 0, texcoord.w != texcoord.x);

	return o;
}
