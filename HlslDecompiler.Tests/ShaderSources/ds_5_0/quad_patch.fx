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
	float3 t0 = lerp(patch[0].position, patch[1].position, sv_domainlocation.x);
	float3 t1 = lerp(patch[3].position, patch[2].position, sv_domainlocation.x);
	float2 t2 = lerp(t0.xz, t1.xz, sv_domainlocation.y);
	float t3 = constants.inside[0] * constants.edges[2] + lerp(t0.y, t1.y, sv_domainlocation.y);
	return mul(float4(t2.x, t3, t2.y, 1), viewProjection);
}
