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

	o.sv_position = patch[0].pos * sv_domainlocation.x + sv_domainlocation.y * patch[1].pos + asfloat(constants.bits) * sv_domainlocation.z;
	o.id = sv_domainlocation.x > 0.5 ? constants.bits ^ patch[0].id : asint(sv_domainlocation.y);

	return o;
}
