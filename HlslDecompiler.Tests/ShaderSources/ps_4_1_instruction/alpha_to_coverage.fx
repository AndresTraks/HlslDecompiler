struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	int4 r0;
	r0.x = asint(texcoord.w * 4);
	r0.x = (uint)asfloat(r0.x);
	r0.yz = int2(0, 0);
	while (true) {
		r0.w = (r0.z >= r0.x) ? -1 : 0;
		if (r0.w != 0) break;
		r0.w = 1 << r0.z;
		r0.y = r0.w | r0.y;
		r0.z = r0.z + 1;
	}
	o.sv_coverage = r0.y;
	o.sv_target.xyz = texcoord.xyz;
	o.sv_target.w = 1;

	return o;
}
