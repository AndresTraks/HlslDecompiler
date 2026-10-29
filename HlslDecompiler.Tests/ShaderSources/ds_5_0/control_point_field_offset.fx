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
	return patch[mode & 1].edge[mode & 3] * sv_domainlocation + patch[(mode >> 1) & 1].edge[((mode >> 2) & 1) + 1] + patch[2].color;
}
