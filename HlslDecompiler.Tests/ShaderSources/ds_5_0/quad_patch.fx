float4x4 viewProjection;

struct DS_IN
{
	float3 position : POSITION;
};

struct DS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

[domain("quad")]
float4 main(DS_CONST constants, float2 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 4> patch) : SV_Position
{
	float3 t0 = lerp(lerp(patch[0].position, patch[1].position, sv_domainlocation.x), lerp(patch[3].position, patch[2].position, sv_domainlocation.x), sv_domainlocation.y);
	float t1 = constants.inside[0] * constants.edges[2] + t0.y;
	return mul(float4(t0.x, t1, t0.z, 1), viewProjection);
}
