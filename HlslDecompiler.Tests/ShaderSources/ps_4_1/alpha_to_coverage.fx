struct PS_OUT
{
	float4 sv_target : SV_Target;
	uint sv_coverage : SV_Coverage;
};

PS_OUT main(float4 texcoord : TEXCOORD)
{
	PS_OUT o;

	int t0 = (uint)(4 * texcoord.w);
	int t1 = 0;
	for (int t2 = 0; t2 < t0; t2 = t2 + 1) {
		t1 = (1 << t2) | t1;
	}
	o.sv_coverage = t1;
	o.sv_target = float4(texcoord.xyz, 1);

	return o;
}
