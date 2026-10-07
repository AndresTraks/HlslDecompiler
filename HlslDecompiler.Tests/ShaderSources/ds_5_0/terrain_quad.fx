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

	float2 t0 = lerp(patch[3].texcoord, patch[2].texcoord, sv_domainlocation.x);
	float2 t1 = lerp(patch[0].texcoord, patch[1].texcoord, sv_domainlocation.x);
	float t2 = heights.SampleLevel(linearSampler, lerp(t1, t0, sv_domainlocation.y), 0).x;
	float3 t3 = lerp(patch[0].position, patch[1].position, sv_domainlocation.x);
	float3 t4 = lerp(patch[3].position, patch[2].position, sv_domainlocation.x);
	float2 t5 = lerp(t3.xz, t4.xz, sv_domainlocation.y);
	float t6 = t2 * amplitude + lerp(t3.y, t4.y, sv_domainlocation.y);
	o.sv_position = mul(float4(t5.x, t6, t5.y, 1), viewProjection);
	o.texcoord = lerp(t1, t0, sv_domainlocation.y);
	o.texcoord1 = saturate(length(float3(t5.x, t6, t5.y) - eye) / fogRange);

	return o;
}
