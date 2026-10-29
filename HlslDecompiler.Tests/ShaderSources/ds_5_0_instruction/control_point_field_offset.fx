cbuffer C : register(b0)
{
	int mode;
};

struct DS_IN
{
	float4 color : COLOR;
	float4 edge[4] : EDGE;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
};

[domain("tri")]
float4 main(DS_CONST constants, float sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	float4 o;

	int4 r0;
	r0.xy = mode >> int2(2, 1);
	r0.xy = r0.xy & int2(1, 1);
	r0.zw = mode & int2(3, 1);
	r0 = asint(patch[r0.w].edge[r0.z] * sv_domainlocation.x + patch[r0.y].edge[r0.x + 1]);
	o = asfloat(r0) + patch[2].color;

	return o;
}
