float4x4 viewProjection;

struct DS_IN
{
	float3 position : POSITION;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

[domain("tri")]
float4 main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	float4 o;

	float4 r0;
	r0.xyz = sv_domainlocation.yyy * patch[1].position.xyz;
	r0.xyz = patch[0].position.xyz * sv_domainlocation.xxx + r0.xyz;
	r0.xyz = patch[2].position.xyz * sv_domainlocation.zzz + r0.xyz;
	r0.w = 1;
	o.x = dot(r0, transpose(viewProjection)[0]);
	o.y = dot(r0, transpose(viewProjection)[1]);
	o.z = dot(r0, transpose(viewProjection)[2]);
	o.w = dot(r0, transpose(viewProjection)[3]);

	return o;
}
