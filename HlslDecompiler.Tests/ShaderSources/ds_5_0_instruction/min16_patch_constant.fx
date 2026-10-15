struct DS_IN
{
	float4 pos : POS;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	min16float scale : SCALE;
};

[domain("tri")]
float4 main(DS_CONST constants, float3 sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	float4 o;

	float4 r0;
	r0 = sv_domainlocation.y * patch[1].pos;
	r0 = patch[0].pos * sv_domainlocation.x + r0;
	r0 = patch[2].pos * sv_domainlocation.z + r0;
	o = r0 * constants.scale.x;

	return o;
}
