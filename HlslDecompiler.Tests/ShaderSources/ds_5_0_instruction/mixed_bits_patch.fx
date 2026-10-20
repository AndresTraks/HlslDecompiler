struct DS_IN
{
	float4 pos : POS;
	uint id : ID;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	uint bits : BITS;
};

struct DS_OUT
{
	float4 sv_position : SV_Position;
	uint id : ID;
};

[domain("tri")]
DS_OUT main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch)
{
	DS_OUT o;

	int4 r0;
	r0 = asint(sv_domainlocation.y * patch[1].pos);
	r0 = asint(patch[0].pos * sv_domainlocation.x + asfloat(r0));
	o.sv_position = asfloat(constants.bits.x) * sv_domainlocation.z + asfloat(r0);
	r0.x = (0.5 < sv_domainlocation.x) ? -1 : 0;
	r0.y = constants.bits.x ^ patch[0].id;
	o.id = (r0.x != 0) ? r0.y : asint(sv_domainlocation.y);

	return o;
}
