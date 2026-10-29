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
	float4 o;

	float4 r0;
	float r1;
	r0.x = constants.mix.x + sv_domainlocation.x;
	r0 = constants.tint * r0.x + constants.tint;
	r1 = (float)(uint)sv_primitiveid;
	o = r0 + r1.x;

	return o;
}
