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
	float4 o;

	float4 r0;
	float3 r1;
	r0.xyz = -(patch[0].tangent.xyz) + patch[1].tangent.xyz;
	r0.xyz = sv_domainlocation.xxx * r0.xyz + patch[0].tangent.xyz;
	r0.w = dot(r0.xyz, r0.xyz);
	r0.w = rsqrt(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r0.w = sv_domainlocation.x * curl.x;
	r0.w = sin(r0.w);
	r0.xyz = r0.www * r0.xyz;
	r1 = -(patch[0].position.xyz) + patch[1].position.xyz;
	r1 = sv_domainlocation.xxx * r1.xyz + patch[0].position.xyz;
	r0.xyz = r0.xyz * curl.yyy + r1.xyz;
	r0.w = 1;
	o.x = dot(r0, transpose(viewProjection)[0]);
	o.y = dot(r0, transpose(viewProjection)[1]);
	o.z = dot(r0, transpose(viewProjection)[2]);
	o.w = dot(r0, transpose(viewProjection)[3]);

	return o;
}
