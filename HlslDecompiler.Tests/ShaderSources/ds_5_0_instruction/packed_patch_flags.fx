struct DS_IN
{
	float4 pos : POS;
};

struct DS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
	uint flags : FLAGS;
};

[domain("quad")]
float4 main(DS_CONST constants, float2 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 4> patch) : SV_Position
{
	float4 o;

	float4 r0;
	int4 r1;
	r0 = -(patch[2].pos) + patch[3].pos;
	r0 = sv_domainlocation.x * r0 + patch[2].pos;
	r1 = asint(-(patch[0].pos) + patch[1].pos);
	r1 = asint(sv_domainlocation.x * asfloat(r1) + patch[0].pos);
	r0 = r0 + -(asfloat(r1));
	r0 = sv_domainlocation.y * r0 + asfloat(r1);
	r1.x = constants.flags.x & 1;
	r1.y = asint(constants.edges[1].x * constants.inside[0].x);
	r1.x = (r1.x != 0) ? r1.y : constants.flags.x;
	o = r0 * asfloat(r1.x) + constants.edges[3].x;

	return o;
}
