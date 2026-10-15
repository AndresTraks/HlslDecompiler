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
	return lerp(lerp(patch[0].pos, patch[1].pos, sv_domainlocation.x), lerp(patch[2].pos, patch[3].pos, sv_domainlocation.x), sv_domainlocation.y) * (constants.flags & 1 ? constants.edges[1].xxxx * constants.inside[0].xxxx : asfloat(constants.flags.xxxx)) + constants.edges[3];
}
