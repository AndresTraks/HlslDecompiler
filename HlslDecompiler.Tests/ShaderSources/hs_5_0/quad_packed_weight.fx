cbuffer C : register(b0)
{
	float detail;
};

struct HS_IN
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_OUT
{
	float3 position : POSITION;
	float weight : WEIGHT;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	o.edges[0] = max(patch[1].weight, patch[0].weight) * detail;
	o.edges[1] = max(patch[2].weight, patch[1].weight) * detail;
	o.edges[2] = max(patch[3].weight, patch[2].weight) * detail;
	o.edges[3] = max(patch[0].weight, patch[3].weight) * detail;
	o.inside[0] = 0.5 * (o.edges[0] + o.edges[2]);
	o.inside[1] = 0.5 * (o.edges[1] + o.edges[3]);

	return o;
}

[domain("quad")]
[partitioning("integer")]
[outputtopology("triangle_cw")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = 2 * patch[sv_outputcontrolpointid].position;
	o.weight = patch[sv_outputcontrolpointid].weight;

	return o;
}
