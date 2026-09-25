cbuffer Params : register(b0)
{
	float4 edgeFactors;
};

struct HS_IN
{
	float3 position : POSITION;
};

struct HS_OUT
{
	float3 position : POSITION;
};

struct HS_CONST
{
	float edges[4] : SV_TessFactor;
	float inside[2] : SV_InsideTessFactor;
};

HS_CONST constants(InputPatch<HS_IN, 4> patch)
{
	HS_CONST o;

	o.edges[0] = edgeFactors.x + patch[0].position.x;
	o.edges[1] = edgeFactors.y + patch[1].position.x;
	o.edges[2] = edgeFactors.z + patch[2].position.x;
	o.edges[3] = edgeFactors.w + patch[3].position.x;
	o.inside[0] = edgeFactors.x;
	o.inside[1] = edgeFactors.y;

	return o;
}

[domain("quad")]
[partitioning("pow2")]
[outputtopology("point")]
[outputcontrolpoints(4)]
[patchconstantfunc("constants")]
HS_OUT main(InputPatch<HS_IN, 4> patch, uint sv_outputcontrolpointid : SV_OutputControlPointID)
{
	HS_OUT o;

	o.position = patch[sv_outputcontrolpointid].position;

	return o;
}
