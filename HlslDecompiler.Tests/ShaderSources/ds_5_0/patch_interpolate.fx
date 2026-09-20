float4x4 viewProjection;

struct DS_IN
{
	float3 position : POSITION;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

[domain("tri")]
float4 main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	float3 t0 = patch[0].position * sv_domainlocation.x + sv_domainlocation.y * patch[1].position + patch[2].position * sv_domainlocation.z;
	return mul(float4(t0, 1), viewProjection);
}
