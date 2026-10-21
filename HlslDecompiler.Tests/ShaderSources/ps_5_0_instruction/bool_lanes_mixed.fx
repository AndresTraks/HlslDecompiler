struct PS_OUT
{
	float4 sv_target : SV_Target;
	float2 sv_target1 : SV_Target1;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	int3 r0;
	r0.x = (0.5 < texcoord.x) ? -1 : 0;
	r0.z = (texcoord.z >= texcoord.y) ? -1 : 0;
	o.sv_target.xz = asfloat(r0.xz & int2(1065353216, 1065353216));
	o.sv_target.y = texcoord.w;
	o.sv_target.w = 1;
	r0.x = (texcoord.z == 0) ? -1 : 0;
	r0.y = (texcoord.w != texcoord.x) ? -1 : 0;
	o.sv_target1 = asfloat(r0.xy & int2(1065353216, 1065353216));

	return o;
}
