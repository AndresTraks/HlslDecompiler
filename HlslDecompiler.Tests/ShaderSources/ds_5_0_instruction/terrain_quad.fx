cbuffer Params : register(b0)
{
	float4x4 viewProjection;
	float3 eye;
	float fogRange;
	float amplitude;
};

SamplerState linearSampler;
Texture2D heights;

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

	float4 r0;
	float4 r1;
	r0.xy = -(patch[3].texcoord.xy) + patch[2].texcoord.xy;
	r0.xy = sv_domainlocation.xx * r0.xy + patch[3].texcoord.xy;
	r0.zw = -(patch[0].texcoord.xy) + patch[1].texcoord.xy;
	r0.zw = sv_domainlocation.xx * r0.zw + patch[0].texcoord.xy;
	r0.xy = -(r0.zw) + r0.xy;
	r0.xy = sv_domainlocation.yy * r0.xy + r0.zw;
	r0.z = heights.SampleLevel(linearSampler, r0.xy, 0).x;
	o.texcoord = r0.xy;
	r0.xyw = -(patch[3].position.xyz) + patch[2].position.xyz;
	r0.xyw = sv_domainlocation.xxx * r0.xyw + patch[3].position.xyz;
	r1.xyz = -(patch[0].position.xyz) + patch[1].position.xyz;
	r1.xyz = sv_domainlocation.xxx * r1.xyz + patch[0].position.xyz;
	r0.xyw = r0.xyw + -(r1.xyz);
	r1.xyz = sv_domainlocation.yyy * r0.xyw + r1.xyz;
	r1.y = r0.z * amplitude + r1.y;
	r1.w = 1;
	o.sv_position.x = dot(r1, transpose(viewProjection)[0]);
	o.sv_position.y = dot(r1, transpose(viewProjection)[1]);
	o.sv_position.z = dot(r1, transpose(viewProjection)[2]);
	o.sv_position.w = dot(r1, transpose(viewProjection)[3]);
	r0.xyz = r1.xyz + -(eye.xyz);
	r0.x = dot(r0.xyz, r0.xyz);
	r0.x = sqrt(r0.x);
	o.texcoord1 = saturate(r0.x / fogRange);

	return o;
}
