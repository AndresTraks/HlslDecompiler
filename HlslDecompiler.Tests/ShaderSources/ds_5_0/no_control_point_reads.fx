struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float mix : MIX;
	float4 tint : TINT;
};

[domain("tri")]
float4 main(DS_CONST constants, float sv_domainlocation : SV_DomainLocation, uint sv_primitiveid : SV_PrimitiveID) : SV_Position
{
	return constants.tint * (constants.mix + sv_domainlocation) + constants.tint + (float)sv_primitiveid;
}
