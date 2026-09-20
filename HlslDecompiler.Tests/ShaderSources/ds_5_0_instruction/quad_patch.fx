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
	float4 o;

	float4 r0;
	float3 r1;
	r0.xyz = -(patch[3].position.xyz) + patch[2].position.xyz;
	r0.xyz = sv_domainlocation.xxx * r0.xyz + patch[3].position.xyz;
	r1 = -(patch[0].position.xyz) + patch[1].position.xyz;
	r1 = sv_domainlocation.xxx * r1.xyz + patch[0].position.xyz;
	r0.xyz = r0.xyz + -(r1.xyz);
	r0.xyz = sv_domainlocation.yyy * r0.xyz + r1.xyz;
	r0.y = constants.inside[0].x * constants.edges[2].x + r0.y;
	r0.w = 1;
	o.x = dot(r0, transpose(viewProjection)[0]);
	o.y = dot(r0, transpose(viewProjection)[1]);
	o.z = dot(r0, transpose(viewProjection)[2]);
	o.w = dot(r0, transpose(viewProjection)[3]);

	return o;
}
