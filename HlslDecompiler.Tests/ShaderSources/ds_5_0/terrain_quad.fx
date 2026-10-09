cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float3 eye;
	float fogRange;
	float amplitude;
};

SamplerState linearSampler;
Texture2D<float> heights;

struct DS_IN
{
	float3 position : POSITION;
	float2 texcoord : TEXCOORD;
};

struct DS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

struct DS_OUT
{
	float4 sv_position : SV_Position;
	float2 texcoord : TEXCOORD;
	float texcoord1 : TEXCOORD1;
};

[domain("quad")]
DS_OUT main(DS_CONST constants, float2 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 4> patch)
{
	DS_OUT o;

	float3 t0 = lerp(lerp(patch[0].position, patch[1].position, sv_domainlocation.x), lerp(patch[3].position, patch[2].position, sv_domainlocation.x), sv_domainlocation.y);
	float2 t1 = lerp(patch[3].texcoord, patch[2].texcoord, sv_domainlocation.x);
	float2 t2 = lerp(patch[0].texcoord, patch[1].texcoord, sv_domainlocation.x);
	float t3 = heights.SampleLevel(linearSampler, lerp(t2, t1, sv_domainlocation.y), 0).x;
	float t4 = t3 * amplitude + t0.y;
	o.sv_position = mul(float4(t0.x, t4, t0.z, 1), viewProjection);
	o.texcoord = lerp(t2, t1, sv_domainlocation.y);
	o.texcoord1 = saturate(length(float3(t0.x, t4, t0.z) - eye) / fogRange);

	return o;
}
