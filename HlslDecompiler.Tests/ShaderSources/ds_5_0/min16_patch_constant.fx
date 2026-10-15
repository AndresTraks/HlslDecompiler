struct DS_IN
{
	float4 pos : POS;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	min16float scale : SCALE;
};

[domain("tri")]
float4 main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	return (patch[0].pos * sv_domainlocation.x + sv_domainlocation.y * patch[1].pos + patch[2].pos * sv_domainlocation.z) * constants.scale;
}
