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

	float4 r0;
	float3 r1;
	float2 r2;
	r0.xyz = sv_domainlocation.yyy * patch[1].position.xyz;
	r0.xyz = patch[0].position.xyz * sv_domainlocation.xxx + r0.xyz;
	r0.xyz = patch[2].position.xyz * sv_domainlocation.zzz + r0.xyz;
	r1 = sv_domainlocation.yyy * patch[1].normal.xyz;
	r1 = patch[0].normal.xyz * sv_domainlocation.xxx + r1.xyz;
	r1 = patch[2].normal.xyz * sv_domainlocation.zzz + r1.xyz;
	r0.w = dot(r1.xyz, r1.xyz);
	r0.w = rsqrt(r0.w);
	r1 = r0.www * r1.xyz;
	r2 = sv_domainlocation.yy * patch[1].texcoord.xy;
	r2 = patch[0].texcoord.xy * sv_domainlocation.xx + r2.xy;
	r2 = patch[2].texcoord.xy * sv_domainlocation.zz + r2.xy;
	r0.w = heightMap.SampleLevel(heightSampler, r2.xy, 0).x;
	o.texcoord = r2.xy;
	r0.w = r0.w * displacement;
	r0.w = r0.w * constants.scale.x;
	r0.xyz = r1.xyz * r0.www + r0.xyz;
	o.normal = r1.xyz;
	r1 = -(r0.xyz) + constants.centre.xyz;
	r0.xyz = r1.xyz * float3(0.00999999978, 0.00999999978, 0.00999999978) + r0.xyz;
	r0.w = 1;
	o.sv_position.x = dot(r0, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r0, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r0, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r0, transpose(viewProjection)[3]);

	return o;
}
