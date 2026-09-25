cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float2 curl;
};

struct DS_IN
{
	float3 position : POSITION;
	float3 tangent : TANGENT;
};

struct DS_CONST
{
	float edges[2] : SV_TessFactor;
};

[domain("isoline")]
float4 main(DS_CONST constants, float sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 2> patch) : SV_Position
{
	float3 t0 = sin(sv_domainlocation * curl.x) * normalize(lerp(patch[0].tangent, patch[1].tangent, sv_domainlocation)) * curl.y + lerp(patch[0].position, patch[1].position, sv_domainlocation);
	return mul(float4(t0, 1), viewProjection);
}
