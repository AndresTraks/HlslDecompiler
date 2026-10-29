cbuffer C : register(b0)
{
	int mode;
};

struct DS_IN
{
	float4 edge[4] : EDGE;
	float4 color : COLOR;
};

struct DS_CONST
{
	float edges[3] : SV_TessFactor;
	float inside : SV_InsideTessFactor;
	float factor : FACTOR;
	float4 weights[4] : WEIGHTS;
};

[domain("tri")]
float4 main(DS_CONST constants, float sv_domainlocation : SV_DomainLocation, const OutputPatch<DS_IN, 3> patch) : SV_Position
{
	return constants.weights[3 * (uint)sv_domainlocation & 3] * constants.factor + patch[mode & 1].edge[mode & 3] + patch[1].color;
}
