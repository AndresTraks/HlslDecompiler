cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float displacement;
};

SamplerState heightSampler;
Texture2D heightMap;

struct DS_IN
{
	float3 position : POSITION;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float3 centre : CENTRE;
	float scale : SCALE;
};

struct DS_OUT
{
	float4 sv_position : SV_Position;
	float3 normal : NORMAL;
	float2 texcoord : TEXCOORD;
};

[domain("tri")]
DS_OUT main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch)
{
	DS_OUT o;

	float3 t0 = patch[0].normal * sv_domainlocation.x + sv_domainlocation.y * patch[1].normal + patch[2].normal * sv_domainlocation.z;
	float2 t1 = patch[0].texcoord * sv_domainlocation.x + sv_domainlocation.y * patch[1].texcoord;
	float2 t2 = patch[2].texcoord * sv_domainlocation.z;
	float t3 = heightMap.SampleLevel(heightSampler, t2 + t1, 0).x * displacement * constants.scale;
	float3 t4 = lerp(patch[0].position * sv_domainlocation.x + sv_domainlocation.y * patch[1].position + patch[2].position * sv_domainlocation.z + normalize(t0) * t3, constants.centre, 0.00999999978);
	o.sv_position = mul(float4(t4, 1), viewProjection);
	o.normal = normalize(t0);
	o.texcoord = t2 + t1;

	return o;
}
